using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public struct Agent
{
    public Vector2 position;
    public float angle;
    public Vector3 deposit;
}

public class Slime : MonoBehaviour
{
    public ComputeShader slimeShader;
    public ComputeShader trailMapProcessShader;
    public ComputeShader kaleidoscopeShader;
    public int size;

    public int sensorSize = 1;
    public float sensorAngleSpacing = 0.3926991f;
    public int sensorOffset = 9;
    public int numAgents;

    [Range(0.01f, 12f)] 
    public float moveSpeed;
    public float turnSpeed;
    [Range(0.1f, 0.99f)] 
    public float evaporateSpeed;
    [Range(0f, 200f)] 
    public float diffusionSpeed;
    [Range(0f, 1f)] 
    public float distanceBias;
    [Range(0, 1f)] 
    public float colorLow;
    [Range(0f, 1f)] 
    public float colorHigh;
    [Range(0f, 1f)] 
    public float colorSaturation;

    public int kaleidoscopeSplit = 4;
    public int iterationsPerPoint = 10;

    public Renderer targetRenderer;
    public int seed;
    private RenderTexture colors;
    private RenderTexture trailMap;
    private RenderTexture processedTrailMap;
    private RenderTexture kaleidoscopeTexture;

    private ComputeBuffer agentBuffer;
    private Agent[] agents;
    private ComputeBuffer randomBuffer;
    private float[] randoms;


    private ComputeBuffer trailBuffer;
    private int slimeHandle;
    private int steerHandle;
    private int processHandle;
    private int colorHandle;
    private int kaleidoscopeKernel;

    private bool doIt = false;
    private Material kaleidoscopeMat;

    // Start is called before the first frame update
    void Awake()
    {
        Random.InitState(seed);
        slimeHandle = slimeShader.FindKernel("CSMain");
        steerHandle = slimeShader.FindKernel("Steer");
        colorHandle = slimeShader.FindKernel("Color");
        processHandle = trailMapProcessShader.FindKernel("ProcessTrailMap");
        kaleidoscopeKernel = kaleidoscopeShader.FindKernel("KalMain");

        Setup();
        targetRenderer.material.mainTexture = colors;
        kaleidoscopeMat = targetRenderer.material;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            doIt = !doIt;
        }
        if(Input.GetKeyDown(KeyCode.G))
        {
            grow = !grow;
        }
        if(Input.GetKeyDown(KeyCode.D))
        {
            dec = !dec;
        }
        if(Input.GetKeyDown(KeyCode.S))
        {
            replicate = !replicate;
        }

        if(doIt)
        {
            if(grow)
            {
                Grow();
            }
            if(dec)
            {
                Dec();
            }
            if(replicate)
            {
                Replicate();
            }
            slimeShader.SetFloat("deltaTime", Time.deltaTime);
            trailMapProcessShader.SetFloat("deltaTime", Time.deltaTime);
            for(int i = 0; i < randoms.Length; i++)
            {
                randoms[i] = Random.Range(0.0f, 1.0f);
            }
            randomBuffer.SetData(randoms);
            slimeShader.SetBuffer(slimeHandle, "randoms", randomBuffer);
            slimeShader.SetBuffer(steerHandle, "randoms2", randomBuffer);
            Compute();
        }
    }

    private bool grow = false;
    private void Grow()
    {
        moveSpeed += (10.0f/60.0f) * Time.deltaTime;
        slimeShader.SetFloat("moveSpeed", moveSpeed);
    }

    private bool dec = false;
    private void Dec()
    {
        diffusionSpeed -= (130.0f/60.0f) * Time.deltaTime;
        slimeShader.SetFloat("diffusionSpeed", diffusionSpeed);
    }

    private bool replicate = false;
    private float replicateN = 8;
    private void Replicate()
    {
        replicateN += (24.0f/60.0f) * Time.deltaTime;
        kaleidoscopeMat.SetFloat("_Replicate", replicateN);
    }

    private void Compute()
    {
        bool swapper = Time.frameCount % 2 == 0;
        slimeShader.SetTexture(slimeHandle, "TrailMap", swapper ? trailMap : processedTrailMap);
        slimeShader.Dispatch(slimeHandle, size / 16, size / 1, 1);
        slimeShader.SetTexture(steerHandle, "TrailMap", swapper ? trailMap : processedTrailMap);
        slimeShader.Dispatch(steerHandle, size / 16, size / 1, 1);
        //slimeShader.SetTexture(slimeHandle, "TrailMap", trailMap);
        //slimeShader.Dispatch(slimeHandle, size / 16, size / 1, 1);
        //slimeShader.SetTexture(steerHandle, "TrailMap", trailMap);
        //slimeShader.Dispatch(steerHandle, size / 16, size / 1, 1);

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
        kaleidoscopeTexture = Util.CreateTexture(size);
        slimeShader.SetTexture(slimeHandle, "TrailMap", trailMap);
        slimeShader.SetTexture(steerHandle, "TrailMap", trailMap);

        slimeShader.SetTexture(colorHandle, "Colors", colors);

        trailMapProcessShader.SetTexture(processHandle, "TrailMap", trailMap);
        trailMapProcessShader.SetTexture(processHandle, "ProcessedTrailMap", processedTrailMap);


        agents = new Agent[numAgents];
        var center = new Vector2(size/2, size/2);
        var palette = colorP;
        for (int i = 0; i < agents.Length; i++)
        {
            Agent agent = agents[i];
            agent.position = (Random.insideUnitCircle * size/3) + center;//new Vector2(, size/2);//new Vector2(Random.Range(0, size), Random.Range(0, size));
            //agent.angle = Random.Range(0.0f, 2*Mathf.PI);
            agent.angle = Vector2.Angle(Vector2.left, (center - agent.position).normalized);
            //agent.deposit = DepositType(i % 6);//*0.65f;
            agent.deposit = FromPalette(palette, i%palette.Length);
            agents[i] = agent;
        }
        agentBuffer = new ComputeBuffer(agents.Length, (sizeof(float) * 6));
        agentBuffer.SetData(agents);
        slimeShader.SetBuffer(slimeHandle, "agents", agentBuffer);
        slimeShader.SetBuffer(steerHandle, "agents", agentBuffer);

        randoms = new float[numAgents];
        for(int i = 0; i < randoms.Length; i++)
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
        switch(i)
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
                return new Vector3(1,0,1);
            case 4:
                return new Vector3(0,1,1);
            case 5:
                return new Vector3(1,1,0);
            default:
                return Vector3.one;

        }
    }

    private Vector3 FromPalette(Color[] palette, int idx)
    {
        return new Vector3(palette[idx].r,palette[idx].g,palette[idx].b);
    }


    private static Color[] colorP => new Color[]{
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

        new Color(0x00,0xBE,0x0B),
        new Color(0xFB,0x56,0x07),
        new Color(0x00,0x00,0x6E),
        //new Color(0x83,0x38,0xEC),
        //new Color(0x3A,0x86,0xFF),
    };

    private static Color FromHex(string hex)
    {
        if(ColorUtility.TryParseHtmlString(hex, out Color c))
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


        kaleidoscopeShader.SetInt("n", kaleidoscopeSplit);
        //rdfShader.SetInt("iterations", iterationsPerPoint);
        targetRenderer.material.mainTexture = colors;
        kaleidoscopeMat = targetRenderer.material;
    }

    private void OnDestroy() {
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
