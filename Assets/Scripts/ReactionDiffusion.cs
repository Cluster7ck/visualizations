using DG.Tweening;
using UnityEngine;
using UnityEditor;
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
    #endregion
    
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
    }
    
    public void OnReset()
    {
        throw new System.NotImplementedException();
    }

    public void OnUnload()
    {
        throw new System.NotImplementedException();
    }

    float time = 0;
    float time2 = 0;
    bool doTime = true;
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            doIt = !doIt;
            //iterationsPerPoint = doIt ? 4 : 1;
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            doTime = !doTime;
        }
        if(doTime)
        {
            time2 += Time.deltaTime;
            rdfShader.SetFloat(timeProp, time2);
        }
        if (doIt)
        {
            time += Time.deltaTime;
            dA = Mathf.Sin(time * 0.127f).Remap(-1, 1, dB+0.13f, 1);
            dB = Mathf.Sin(time).Remap(-1, 1, 0.28f, 0.535f);
            feed = Mathf.Sin(time * 0.1137f).Remap(-1, 1, 0.028f, 0.062f);
            k = Mathf.Sin(time * 0.137f).Remap(-1, 1, 0.057f, 0.064f);
            
            SetValues();
            //rdfShader.SetFloat(dAProp, dA);
            //rdfShader.SetFloat(dBProp, dB);
            //rdfShader.SetFloat(feedProp, feed);
            //rdfShader.SetFloat(kProp, k);
            //rdfShader.SetFloat(mProp, m);
        }

        if (Input.GetKeyDown(KeyCode.H))
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
        kaleidoscopeMat.DOFloat(4, "_Zoom", 1f).SetEase(kaleidoscopePushCurve);
    }
    
    private void OnSnare()
    {
        kaleidoscopeMat.DOFloat(32, replicateProp, 1f).SetEase(kaleidoscopePushCurve);
    }

    private void OnDisable()
    {
        cellBuffer?.Release();
        cellBuffer = null;
        if(texture) texture.Release();
        texture = null;
    }

    private void OnDestroy()
    {
        cellBuffer?.Release();
        cellBuffer = null;
        if(texture) texture.Release();
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