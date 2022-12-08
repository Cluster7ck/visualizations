using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public struct Agent
{
    public Vector2 position;
    public float angle;
    public Vector3 deposit;
}

public class Slime : MonoBehaviour, ILifecycleReceiver
{
    public ComputeShader slimeShader;
    public ComputeShader trailMapProcessShader;
    public int size;

    public int sensorSize = 1;
    public float sensorAngleSpacing = 0.3926991f;
    public int sensorOffset = 9;
    public int numAgents;

    [Range(0.01f, 12f)] public float moveSpeed;
    public float turnSpeed;
    [Range(0.1f, 0.99f)] public float evaporateSpeed;
    [Range(0f, 200f)] public float diffusionSpeed;
    [Range(0f, 1f)] public float distanceBias;
    [Range(0, 1f)] public float colorLow;
    [Range(0f, 1f)] public float colorHigh;
    [Range(0f, 1f)] public float colorSaturation;

    [Header("Reactive Config")]
    [Header("OnKick")]
    // dont break this shit
    [SerializeField]
    private AnimationCurve kaleidoscopePushCurve;

    [SerializeField] private float zoomFactor;
    [SerializeField] private float zoomTime;
    [SerializeField] private float zoomDefault;

    [Header("OnSnare")]
    // dont break this shit
    [SerializeField]
    private float moveSpeedDefault;

    [SerializeField] private float moveSpeedFactor;
    [SerializeField] private float moveSpeedTime;
    [SerializeField] private AnimationCurve moveSpeedPushCurve;
    [SerializeField] private float evaporateSpeedDefault;
    [SerializeField] private float evaporateFactor;
    [SerializeField] private AnimationCurve evaporateSpeedPushCurve;

    [Header("OnHats")]
    // dont break this shit
    [SerializeField]
    private float diffusionDefault;

    [SerializeField] private float diffusionSubtract;
    [SerializeField] private float diffusionDecreasingSpeed;
    [SerializeField] private float diffusionTime;
    private bool diffusionDecreasing;
    
    [SerializeField] private float rotationSpeed = 1.0f;
    [SerializeField] private float rotationSpeedDefault = 1;
    [SerializeField] private float rotationSpeedKick = 120.0f;

    private float kaleidRotation;

    public Renderer targetRenderer;
    public int seed;
    private RenderTexture colors;
    private RenderTexture trailMap;
    private RenderTexture processedTrailMap;

    private ComputeBuffer agentBuffer;
    private Agent[] agents;
    private ComputeBuffer randomBuffer;
    private float[] randoms;

    private ComputeBuffer trailBuffer;
    private int slimeHandle;
    private int steerHandle;
    private int processHandle;
    private int colorHandle;

    private bool doIt = false;
    private Material kaleidoscopeMat;
    private bool initalized;

    private bool replicate = false;
    private float replicateN = 8;
    private OSC osc;

    private Sequence onSnareSeq;
    private Sequence onKickSeq;
    
    private TweenerCore<float, float, FloatOptions> zoomTweener;
    private TweenerCore<float, float, FloatOptions> moveSpeedTweener;
    private TweenerCore<float, float, FloatOptions> diffusionTweener;

    #region Shader props

    private static readonly int timeProp = Shader.PropertyToID("deltaTime");
    private static readonly int zoomProp = Shader.PropertyToID("_Zoom");
    private static readonly int rotationProp = Shader.PropertyToID("_Rotation");

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
        Random.InitState(seed);
        slimeHandle = slimeShader.FindKernel("CSMain");
        steerHandle = slimeShader.FindKernel("Steer");
        colorHandle = slimeShader.FindKernel("Color");
        processHandle = trailMapProcessShader.FindKernel("ProcessTrailMap");

        Setup();
        targetRenderer.material.mainTexture = colors;
        kaleidoscopeMat = targetRenderer.material;

        if (osc != null)
        {
            this.osc = osc;
            osc.SetAddressHandler("/kick", OnKickMsg);
            osc.SetAddressHandler("/snare", OnSnareMsg);
            osc.SetAddressHandler("/hihat", OnHatsMsg);
        }
        initalized = true;
    }

    public void OnReset() { }

    public void OnUnload()
    {
        if (osc)
        {
            osc.RemoveAllMessageHandlers();
        }
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

    private void OnKick()
    {
        zoomTweener?.Kill();
        kaleidoscopeMat.SetFloat(zoomProp, zoomDefault);
        rotationSpeed = rotationSpeedKick;
        zoomTweener = kaleidoscopeMat
            .DOFloat(zoomFactor, zoomProp, zoomTime)
            .SetEase(kaleidoscopePushCurve)
            .OnComplete(() => rotationSpeed = rotationSpeedDefault)
            .OnKill(() => rotationSpeed = rotationSpeedDefault);
    }

    private void OnSnare()
    {
        onSnareSeq?.Kill();
        moveSpeed = moveSpeedDefault;
        slimeShader.SetFloat("moveSpeed", moveSpeedDefault);
        evaporateSpeed = evaporateSpeedDefault;
        trailMapProcessShader.SetFloat("evaporateSpeed", evaporateSpeedDefault);

        onSnareSeq = DOTween.Sequence()
            .Append(
                DOTween.To(() => moveSpeed,
                           x =>
                           {
                               moveSpeed = x;
                               slimeShader.SetFloat("moveSpeed", moveSpeed);
                           },
                           moveSpeedFactor,
                           moveSpeedTime)
                    .SetEase(moveSpeedPushCurve)
            )
            .Join(
                DOTween.To(() => evaporateSpeed,
                           x =>
                           {
                               evaporateSpeed = x;
                               trailMapProcessShader.SetFloat("evaporateSpeed", evaporateSpeed);
                           },
                           evaporateFactor,
                           moveSpeedTime)
                    .SetEase(evaporateSpeedPushCurve)
            )
            .Play();
    }

    private void OnHihats()
    {
        diffusionTweener?.Kill();

        var endvalue = diffusionSpeed - diffusionSubtract;
        diffusionDecreasing = true;
        diffusionTweener = DOTween.To(() => diffusionSpeed,
                                      x =>
                                      {
                                          x = Mathf.Clamp01(x);
                                          diffusionSpeed = x;
                                      },
                                      endvalue,
                                      diffusionTime)
            .OnUpdate(() => trailMapProcessShader.SetFloat("diffuseSpeed", diffusionSpeed))
            .OnComplete(() => diffusionDecreasing = false)
            .OnKill(() => diffusionDecreasing = false);
    }

    // Update is called once per frame
    void Update()
    {
        if (!initalized) return;

        slimeShader.SetFloat(timeProp, Time.deltaTime);
        trailMapProcessShader.SetFloat(timeProp, Time.deltaTime);
        for (int i = 0; i < randoms.Length; i++)
        {
            randoms[i] = Random.Range(0.0f, 1.0f);
        }
        randomBuffer.SetData(randoms);
        slimeShader.SetBuffer(slimeHandle, "randoms", randomBuffer);
        slimeShader.SetBuffer(steerHandle, "randoms2", randomBuffer);

        if (diffusionSpeed < diffusionDefault && !diffusionDecreasing)
        {
            diffusionSpeed += Time.deltaTime * diffusionDecreasingSpeed;
            diffusionSpeed = Mathf.Clamp(diffusionSpeed, 0, diffusionDefault);
            trailMapProcessShader.SetFloat("diffusionSpeed", diffusionSpeed);
        }

        // rotate the whole thing
        kaleidRotation += Time.deltaTime * rotationSpeed;
        kaleidoscopeMat.SetFloat(rotationProp, kaleidRotation);
        
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

        Compute();
    }

    private void Compute()
    {
        bool swapper = Time.frameCount % 2 == 0;
        slimeShader.SetTexture(slimeHandle, "TrailMap", swapper ? trailMap : processedTrailMap);
        slimeShader.Dispatch(slimeHandle, size / 16, size / 1, 1);
        slimeShader.SetTexture(steerHandle, "TrailMap", swapper ? trailMap : processedTrailMap);
        slimeShader.Dispatch(steerHandle, size / 16, size / 1, 1);

        trailMapProcessShader.SetTexture(processHandle, "TrailMap", swapper ? trailMap : processedTrailMap);
        trailMapProcessShader.SetTexture(processHandle, "ProcessedTrailMap", swapper ? processedTrailMap : trailMap);
        trailMapProcessShader.Dispatch(processHandle, size / 8, size / 8, 1);

        slimeShader.SetTexture(colorHandle, "TrailMap", swapper ? processedTrailMap : trailMap);
        slimeShader.Dispatch(colorHandle, size / 16, size / 1, 1);
    }

    private void Setup()
    {
        colors = Util.CreateTexture(size);
        trailMap = Util.CreateTexture(size);
        processedTrailMap = Util.CreateTexture(size);
        slimeShader.SetTexture(slimeHandle, "TrailMap", trailMap);
        slimeShader.SetTexture(steerHandle, "TrailMap", trailMap);

        slimeShader.SetTexture(colorHandle, "Colors", colors);

        trailMapProcessShader.SetTexture(processHandle, "TrailMap", trailMap);
        trailMapProcessShader.SetTexture(processHandle, "ProcessedTrailMap", processedTrailMap);

        agents = new Agent[numAgents];
        var center = new Vector2(size / 2, size / 2);
        var palette = colorP;
        for (int i = 0; i < agents.Length; i++)
        {
            var agent = agents[i];
            agent.position = (Random.insideUnitCircle * size / 3) + center;
            agent.angle = Random.Range(0.0f, 2 * Mathf.PI);
            //agent.angle = Vector2.Angle(Vector2.left, (center - agent.position).normalized);
            //agent.deposit = DepositType(i % 6); //*0.65f;
            agent.deposit = FromPalette(palette, i%palette.Length);
            agents[i] = agent;
        }
        agentBuffer = new ComputeBuffer(agents.Length, (sizeof(float) * 6));
        agentBuffer.SetData(agents);
        slimeShader.SetBuffer(slimeHandle, "agents", agentBuffer);
        slimeShader.SetBuffer(steerHandle, "agents", agentBuffer);

        randoms = new float[numAgents];
        for (int i = 0; i < randoms.Length; i++)
        {
            randoms[i] = Random.Range(0.0f, 1.0f);
        }
        randomBuffer = new ComputeBuffer(randoms.Length, sizeof(float));
        randomBuffer.SetData(randoms);
        slimeShader.SetBuffer(slimeHandle, "randoms", randomBuffer);
        slimeShader.SetBuffer(steerHandle, "randoms", randomBuffer);

        SetValues();
    }

    private Vector3 DepositType(int i)
    {
        switch (i)
        {
            /*
            case 0: 
                return new Vector3(1,0,1);
            case 1:
                return new Vector3(0,1,1);
            case 2:
                return new Vector3(1,1,0);
                */

            case 0:
                return Vector3.right;
            case 1:
                return Vector3.up;
            case 2:
                return Vector3.forward;
            case 3:
                return new Vector3(1, 0, 1);
            case 4:
                return new Vector3(0, 1, 1);
            case 5:
                return new Vector3(1, 1, 0);
            default:
                return Vector3.one;
        }
    }

    private Vector3 FromPalette(Color[] palette, int idx)
    {
        return new Vector3(palette[idx].r, palette[idx].g, palette[idx].b);
    }


    private static Color[] colorP => new Color[]
    {
        /*
        FromHex("#FFBE0B"),
        FromHex("#FB5607"),
        FromHex("#FF006E"),
        FromHex("#8338EC"),
        FromHex("#3A86FF"),
        */
        //new Color(0xFF,0x00,0x00),
        //new Color(0x00,0xFF,0x00),
        //new Color(0x00,0x22,0xEE),

        new Color(0x00, 0xBE, 0x0B),
        new Color(0xFB, 0x56, 0x07),
        new Color(0x00, 0x00, 0x6E),
        //new Color(0x83,0x38,0xEC),
        //new Color(0x3A,0x86,0xFF),
    };

    private static Color FromHex(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color c))
        {
            return c;
        }
        return Color.white;
    }


    private void SetValues()
    {
        slimeShader.SetFloat("sensorAngleSpacing", sensorAngleSpacing);
        slimeShader.SetInt("sensorSize", sensorSize);
        slimeShader.SetFloat("sensorOffset", sensorOffset);
        slimeShader.SetFloat("moveSpeed", moveSpeed);
        slimeShader.SetFloat("turnSpeed", turnSpeed);
        slimeShader.SetFloat("evaporateSpeed", evaporateSpeed);
        slimeShader.SetInt("width", size);
        slimeShader.SetInt("height", size);

        slimeShader.SetInt("numAgents", numAgents);

        slimeShader.SetFloat("colorLow", colorLow);
        slimeShader.SetFloat("colorHigh", colorHigh);
        slimeShader.SetFloat("colorSaturation", colorSaturation);
        slimeShader.SetFloat("distanceBias", distanceBias);

        trailMapProcessShader.SetInt("width", size);
        trailMapProcessShader.SetInt("height", size);
        trailMapProcessShader.SetFloat("evaporateSpeed", evaporateSpeed);
        trailMapProcessShader.SetFloat("diffuseSpeed", diffusionSpeed);

        targetRenderer.material.mainTexture = colors;
        kaleidoscopeMat = targetRenderer.material;
    }

    private void OnDestroy()
    {
        agentBuffer.Release();
        randomBuffer.Release();
        trailMap.Release();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (EditorApplication.isPlaying)
            SetValues();
    }
#endif
}

public static class Util
{
    public static RenderTexture CreateTexture(int size)
    {
        var texture = new RenderTexture(size, size, 24);
        texture.enableRandomWrite = true;
        texture.Create();
        return texture;
    }
}