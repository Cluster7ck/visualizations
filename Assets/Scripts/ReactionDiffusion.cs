using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ReactionDiffusion : MonoBehaviour
{
    public ComputeShader rdfShader;
    public ComputeShader kaleidoscopeShader;
    public int sizeL;

    [Range(0, 1)] public float dA = 1.0f;
    [Range(0, 1)] public float dB = 0.5f;
    [Range(0, 0.3f)] public float feed = 0.055f;
    [Range(0, 0.4f)] public float k = 0.062f;
    [Range(0, 1)] public float m = 0.2f;

    public int kaleidoscopeSplit = 4;

    public int circlePoints;
    public int pointWidth;
    public int iterationsPerPoint = 10;

    private int kernelHandle;
    private int kaleidoscopeKernel;

    public Renderer rend;
    private RenderTexture texture;
    public RenderTexture kaleidoscopeTexture;

    private ComputeBuffer cellBuffer;
    private Vector2[] grid;
    [SerializeField] private bool doIt;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 300;
        kernelHandle = rdfShader.FindKernel("RDFMain");
        kaleidoscopeKernel = kaleidoscopeShader.FindKernel("KalMain");
        grid = new Vector2[sizeL * sizeL];
        //prev = new Cell[width][height];

        var bla = (sizeL * sizeL) * 1.0f;
        for (int x = 0; x < sizeL; x++)
        {
            for (int y = 0; y < sizeL; y++)
            {
                float a = 1; //(x + y * sizeL) / bla;//1;
                float b = 0;
                grid[x + y * sizeL] = new Vector2(a, b);
                //prev[i][j] = new Cell(a, b);
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

        CreateDumbStuff();
        SetValues();
    }

    private void Start()
    {
        rend.material.mainTexture = kaleidoscopeTexture;
        rdfShader.SetTexture(kernelHandle, "Result", kaleidoscopeTexture);

        cellBuffer = new ComputeBuffer(grid.Length, 8);
        cellBuffer.SetData(grid);

        rdfShader.SetInt("sizeL", sizeL);
        rdfShader.SetBuffer(kernelHandle, "Cells", cellBuffer);

        kaleidoscopeShader.SetInt("sizeL", sizeL);
        kaleidoscopeShader.SetInt("n", kaleidoscopeSplit);
        kaleidoscopeShader.SetTexture(kernelHandle, "Input", texture);
        kaleidoscopeShader.SetTexture(kernelHandle, "Result", kaleidoscopeTexture);
        Shader.WarmupAllShaders();
        Compute();
    }

    private void CreateDumbStuff()
    {
        texture = Util.CreateTexture(sizeL);
        kaleidoscopeTexture = Util.CreateTexture(sizeL);
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
            rdfShader.SetFloat("time", time2);
        }
        if (doIt)
        {
             
            time += Time.deltaTime;
            dB = map(Mathf.Sin(time), -1, 1, 0.28f, 0.535f);
            dA = map(Mathf.Sin(time * 0.127f), -1, 1, dB+0.13f, 1);
            feed = map(Mathf.Sin(time * 0.1137f), -1, 1, 0.028f, 0.062f);
            k = map(Mathf.Sin(time * 0.137f), -1, 1, 0.057f, 0.064f);
            rdfShader.SetFloat("dA", dA);
            rdfShader.SetFloat("dB", dB);
            rdfShader.SetFloat("feed", feed);
            rdfShader.SetFloat("k", k);
            rdfShader.SetFloat("m", m);
            kaleidoscopeShader.SetInt("n", kaleidoscopeSplit);
        }
        for (int i = 0; i < iterationsPerPoint; i++)
            Compute();
    }

    private void Compute()
    {
        /*
        texture.Release();
        texture = new RenderTexture(sizeL, sizeL, 24);
        texture.enableRandomWrite = true;
        texture.Create();
        rend.material.mainTexture = texture;
        shader.SetTexture(kernelHandle, "Result", texture);
        */

        rdfShader.Dispatch(kernelHandle, sizeL / 32, sizeL / 32, 1);
        //kaleidoscopeShader.Dispatch(kaleidoscopeKernel, sizeL / 32, sizeL / 32, 1);

        //rdfShader.SetInt("centerY", Mathf.FloorToInt(map(Screen.height - Input.mousePosition.y,0,Screen.height,0,sizeL)));
        //rdfShader.SetInt("centerX", Mathf.FloorToInt(map(Screen.width - Input.mousePosition.x,0,Screen.width,0,sizeL)));
        //rdfShader.SetInt("centerX",sizeL/2);
    }

    float map(float s, float a1, float a2, float b1, float b2)
    {
        return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
    }

    private void OnDisable()
    {
        cellBuffer.Release();
        texture.Release();
    }

    private void OnDestroy()
    {
        cellBuffer.Release();
        texture.Release();
    }
    private void SetValues()
    {
        rdfShader.SetFloat("dA", dA);
        rdfShader.SetFloat("dB", dB);
        rdfShader.SetFloat("feed", feed);
        rdfShader.SetFloat("k", k);
        rdfShader.SetInt("centerY", sizeL / 2);
        rdfShader.SetInt("centerX", sizeL / 2);
        rdfShader.SetFloat("m", m);
        kaleidoscopeShader.SetInt("n", kaleidoscopeSplit);
        //rdfShader.SetInt("iterations", iterationsPerPoint);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (EditorApplication.isPlaying)
            SetValues();
    }
#endif
}