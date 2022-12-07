using System.Data;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

public class ReactionDiffusion : MonoBehaviour, ILifecycleReceiver
{
    public ComputeShader rdfShader;
    public int sizeL;

    [Range(0, 1)] public float dA = 1.0f;
    [Range(0, 1)] public float dB = 0.5f;
    [Range(0, 0.3f)] public float feed = 0.055f;
    [Range(0, 0.4f)] public float k = 0.062f;
    [Range(0, 1)] public float m = 0.2f;

    public int circlePoints;
    public int pointWidth;
    public int iterationsPerPoint = 10;

    [SerializeField] private AnimationCurve kaleidoscopePushCurve;
    [SerializeField] private float zoomFactor;
    [SerializeField] private float zoomTime;
    [SerializeField] private float zoomDefault;

    [SerializeField] private AnimationCurve replicatePushCurve;
    [SerializeField] private float replicateFactor;
    [SerializeField] private float replicateTime;
    [SerializeField] private float replicateDefault;
    [SerializeField] private float replicateDecreasingSpeed;

    [SerializeField] private float paletteTime;
    private float paletteBlend;
    private bool goingUp;

    [ReadOnly] public float replicateness = 4;
    private bool replicateIncreasing = false;

    private int reactionDiffusionKernelHandle;

    public Renderer rend;
    private RenderTexture texture;

    private Material kaleidoscopeMat;
    private ComputeBuffer cellBuffer;
    private Vector2[] grid;
    [SerializeField] private bool doIt;

    #region Shader props

    private static readonly int timeProp = Shader.PropertyToID("time");
    private static readonly int sizeProp = Shader.PropertyToID("sizeL");
    private static readonly int resultProp = Shader.PropertyToID("Result");
    private static readonly int cellsProp = Shader.PropertyToID("Cells");

    private static readonly int dAProp = Shader.PropertyToID("dA");
    private static readonly int dBProp = Shader.PropertyToID("dB");
    private static readonly int feedProp = Shader.PropertyToID("feed");
    private static readonly int kProp = Shader.PropertyToID("k");
    private static readonly int mProp = Shader.PropertyToID("m");
    private static readonly int centerXProp = Shader.PropertyToID("centerX");
    private static readonly int centerYProp = Shader.PropertyToID("centerY");

    private static readonly int replicateProp = Shader.PropertyToID("_Replicate");
    private static readonly int zoomProp = Shader.PropertyToID("_Zoom");

    #endregion

    private TweenerCore<float, float, FloatOptions> zoomTweener;
    private TweenerCore<float, float, FloatOptions> replicateTweener;
    private TweenerCore<float, float, FloatOptions> paletteTweener;

    private bool initialized;

    void Awake()
    {
        var scene = SceneManager.GetSceneByName("StartUp");
        if (!scene.IsValid())
        {
            OnInit(null);
        }
    }

    public void OnInit(OSC osc)
    {
        if (initialized)
        {
            Debug.LogError("This should not happen");
            return;
        }
        
        if (osc != null)
        {
            osc.SetAddressHandler("/kick", OnKickMsg);
            osc.SetAddressHandler("/snare", OnSnareMsg);
            osc.SetAddressHandler("/hihat", OnHatsMsg);
        }
        
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 300;
        reactionDiffusionKernelHandle = rdfShader.FindKernel("RDFMain");
        grid = new Vector2[sizeL * sizeL];

        for (int x = 0; x < sizeL; x++)
        {
            for (int y = 0; y < sizeL; y++)
            {
                float a = 1;
                float b = 0;
                grid[x + y * sizeL] = new Vector2(a, b);
            }
        }

        int e = circlePoints;
        var p = (2 * Mathf.PI) / e;
        float colorStep = 1.0f / e;

        for (int ring = 0; ring < pointWidth; ring++)
        {
            for (int n = 0; n < e; n++)
            {
                int x = Mathf.FloorToInt(((sizeL / 4) - ring) * Mathf.Cos(p * n) + (sizeL / 2));
                int y = Mathf.FloorToInt(((sizeL / 4) - ring) * Mathf.Sin(p * n) + (sizeL / 2));

                grid[x + y * sizeL] = new Vector2(1, 1);
            }
        }

        int sizeBox = 40;
        for (int n = 0; n < 10; n++)
        {
            for (int x = sizeL / 2 - sizeBox / 2; x < sizeL / 2 + sizeBox / 2; x++)
            {
                for (int y = sizeL / 2 - sizeBox / 2; y < sizeL / 2 + sizeBox / 2; y++)
                {
                    grid[x + y * sizeL] = new Vector2(1, 1);
                }
            }
            sizeBox -= 2;
        }

        texture = Util.CreateTexture(sizeL);
        SetValues();

        kaleidoscopeMat = rend.material;
        rend.material.mainTexture = texture;
        rdfShader.SetTexture(reactionDiffusionKernelHandle, resultProp, texture);

        cellBuffer = new ComputeBuffer(grid.Length, 8);
        cellBuffer.SetData(grid);

        rdfShader.SetInt(sizeProp, sizeL);
        rdfShader.SetBuffer(reactionDiffusionKernelHandle, cellsProp, cellBuffer);

        Shader.WarmupAllShaders();
        Compute();
        initialized = true;
    }
    
    private void OnKickMsg(OscMessage msg)
    {
        OnKick();
    }
    private void OnSnareMsg(OscMessage msg)
    {
        OnSnare();
    }
    private void OnHatsMsg(OscMessage msg)
    {
        OnHihats();
    }

    public void OnReset() { }

    public void OnUnload() { }

    float time = 0;
    float time2 = 0;

    bool doTime = true;

    // Update is called once per frame
    void Update()
    {
        if (!initialized) return;
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            doIt = !doIt;
            //iterationsPerPoint = doIt ? 4 : 1;
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            doTime = !doTime;
        }
        if (doTime)
        {
            time2 += Time.deltaTime;
            rdfShader.SetFloat(timeProp, time2);
        }
        if (doIt)
        {
            time += Time.deltaTime;
            dA = Mathf.Sin(time * 0.127f).Remap(-1, 1, dB + 0.13f, 1);
            dB = Mathf.Sin(time).Remap(-1, 1, 0.28f, 0.535f);
            feed = Mathf.Sin(time * 0.1137f).Remap(-1, 1, 0.028f, 0.062f);
            k = Mathf.Sin(time * 0.137f).Remap(-1, 1, 0.057f, 0.064f);

            SetValues();
        }

        if (replicateness > replicateDefault && !replicateIncreasing)
        {
            replicateness -= Time.deltaTime * replicateDecreasingSpeed;
            replicateness = Mathf.Clamp(replicateness, replicateDefault, 64);
            kaleidoscopeMat.SetFloat(replicateProp, replicateness);
        }

        rdfShader.SetFloat("palette", paletteBlend);
        if (Input.GetKeyDown(KeyCode.H))
        {
            OnHihats();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            OnSnare();
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            OnKick();
        }

        for (int i = 0; i < iterationsPerPoint; i++)
        {
            Compute();
        }
    }

    private void Compute()
    {
        rdfShader.Dispatch(reactionDiffusionKernelHandle, sizeL / 32, sizeL / 32, 1);
    }

    private void OnKick()
    {
        zoomTweener?.Kill();
        kaleidoscopeMat.SetFloat(zoomProp, zoomDefault);
        zoomTweener = kaleidoscopeMat.DOFloat(zoomFactor, zoomProp, zoomTime).SetEase(kaleidoscopePushCurve);
    }

    private void OnSnare()
    {
        Debug.Log("OnSnare");
        float endValue = goingUp ? 0 : 1;
        goingUp = !goingUp;
        
        paletteTweener?.Kill();
        paletteTweener = DOTween.To(() => paletteBlend,
                                    x =>
                                    {
                                        paletteBlend = x;
                                        rdfShader.SetFloat("palette", paletteBlend);
                                    },
                                    endValue,
                                    paletteTime).SetEase(Ease.InOutCubic);
    }

    private void OnHihats()
    {
        replicateTweener?.Kill();

        var endvalue = replicateness + 4f;
        replicateIncreasing = true;
        replicateTweener = DOTween.To(() => replicateness, x => replicateness = x, endvalue, replicateTime)
            .OnUpdate(() => kaleidoscopeMat.SetFloat(replicateProp, replicateness))
            .OnComplete(() => replicateIncreasing = false)
            .OnKill(() => replicateIncreasing = false);
    }

    private void OnDisable()
    {
        cellBuffer?.Release();
        cellBuffer = null;
        if (texture) texture.Release();
        texture = null;
    }

    private void OnDestroy()
    {
        cellBuffer?.Release();
        cellBuffer = null;
        if (texture) texture.Release();
        texture = null;
    }

    private void SetValues()
    {
        rdfShader.SetFloat(dAProp, dA);
        rdfShader.SetFloat(dBProp, dB);
        rdfShader.SetFloat(feedProp, feed);
        rdfShader.SetFloat(kProp, k);
        rdfShader.SetInt(centerYProp, sizeL / 2);
        rdfShader.SetInt(centerXProp, sizeL / 2);
        rdfShader.SetFloat(mProp, m);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (EditorApplication.isPlaying)
            SetValues();
    }
#endif
}