using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoShaderCompute : MonoBehaviour
{
    public ComputeShader shader;
    public Renderer rend;
    public int e;

    [Range(0,1)]
    public float weight;
    public bool stop;

    private int[] randomIndices = new int[1024 * 1024];

    private RenderTexture texture;
    private ComputeBuffer randIdx;
    private ComputeBuffer pointBuffer;
    private ComputeBuffer colorBuffer;
    private int kernelHandle;

    void Update()
    {
        if(stop) return;
        Compute();
        weight += 0.002f;
        if(weight >= 1)
        {
            weight = 0.01f;
            e++;
            Release();
            Setup();
        }
    }

    private void Setup()
    {
        var points = new List<Vector2>(); 
        var colors = new List<Color>();

        var p = (2* Mathf.PI) / e;
        float colorStep = 1.0f / e; 

        for (int n = 0; n < e; n++)
        {
            //var ra = Random.Range(0, 2 * Mathf.PI);
            var x = 256 * Mathf.Cos(p*n) + 512;
            var y = 256 * Mathf.Sin(p*n) + 512;
            points.Add(new Vector2(x,y));
            colors.Add(Color.HSVToRGB(n*colorStep,1,1));
        }

        randomIndices[0] =  Random.Range(0, points.Count);
        randomIndices[1] =  Random.Range(0, points.Count);
        for (int i = 2; i < randomIndices.Length; i++)
        {
            int randv = -1;
            do {
                randv =  Random.Range(0, points.Count);
            } while(randv == randomIndices[i-2]);
            randomIndices[i] = randv;
        }

        randIdx = new ComputeBuffer(randomIndices.Length, 4);
        pointBuffer = new ComputeBuffer(points.Count, 8);
        colorBuffer = new ComputeBuffer(colors.Count, 16);

        randIdx.SetData(randomIndices);
        pointBuffer.SetData(points.ToArray());
        colorBuffer.SetData(colors.ToArray());

        texture = new RenderTexture(1024, 1024, 24);
        texture.enableRandomWrite = true;
        texture.Create();
        rend.material.mainTexture = texture;
        shader.SetTexture(kernelHandle, "Result", texture);

        shader.SetBuffer(kernelHandle, "Input", randIdx);
        shader.SetBuffer(kernelHandle, "Points", pointBuffer);
        shader.SetBuffer(kernelHandle, "Colors", colorBuffer);
    }

    private void Awake()
    {
        kernelHandle = shader.FindKernel("CSMain");
        Setup();
    }

    private void Compute()
    {
        texture.Release();
        texture = new RenderTexture(1024, 1024, 24);
        texture.enableRandomWrite = true;
        texture.Create();
        rend.material.mainTexture = texture;
        shader.SetTexture(kernelHandle, "Result", texture);

        shader.SetFloat("weight", weight);
        shader.Dispatch(kernelHandle, 1024 / 16, 1024 / 16, 1);
    }

    private void OnDisable() {
        Release();
    }
    private void Release()
    {
        randIdx.Release();
        pointBuffer.Release();
        texture.Release();
        colorBuffer.Release();
    }
}