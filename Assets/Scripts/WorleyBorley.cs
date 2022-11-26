using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class WorleyBorley : MonoBehaviour
{
    public ComputeShader worleyShader;
    public int sizeL;
    public Renderer rend;
    [Header("Stuff")]
    public int pointAmount;
    public float distMap;
    public float reChance;

    private int kernelHandle;
    private RenderTexture texture;

    private ComputeBuffer pointBuffer;
    private Boingo[] boingos;
    private Vector2[] points;
    // Start is called before the first frame update
    void Awake()
    {
        kernelHandle = worleyShader.FindKernel("CSMain");

        texture = new RenderTexture(sizeL, sizeL, 24);
        texture.enableRandomWrite = true;
        texture.Create();
        rend.material.mainTexture = texture;
        worleyShader.SetTexture(kernelHandle, "Result", texture);

        points = new Vector2[pointAmount];
        boingos = new Boingo[pointAmount];
        for(int i = 0; i < points.Length; i++)
        {
            var randomVec = new Vector2(Random.Range(0,sizeL),Random.Range(0,sizeL));
            points[i] = randomVec;
            boingos[i] = new Boingo{
                position = randomVec,
                velocity = Random.insideUnitCircle,
                speed  = 1
            };
            Debug.Log(points[i]);
        }

        pointBuffer = new ComputeBuffer(points.Length, 8);
        pointBuffer.SetData(points);
        worleyShader.SetBuffer(kernelHandle, "Points", pointBuffer);
    }

    // Update is called once per frame
    void Update()
    {
        MovePoints();
        worleyShader.Dispatch(kernelHandle, sizeL / 16, sizeL / 16, 1);
    }

    private void MovePoints()
    {
        for(int i = 0; i < boingos.Length; i++)
        {
            var b = boingos[i];
            var newPos = b.position + b.velocity;
            if(newPos.x <= 0)
            {
                b.velocity = b.velocity * Vector2.left;
                newPos += b.velocity;
            }
            else if(newPos.y <= 0)
            {
                b.velocity = b.velocity * Vector2.down;
                newPos += b.velocity;
            }
            else if(newPos.x >= sizeL)
            {
                b.velocity = b.velocity * Vector2.left;
                newPos += b.velocity;
            }
            else if(newPos.y >= sizeL)
            {
                b.velocity = b.velocity * Vector2.down;
                newPos += b.velocity;
            }
            if(Random.value < reChance)
            {
                int dir = Random.value >= 0.5f ? 1 : -1;
                var theta = Random.Range(5,15) * Mathf.Deg2Rad * dir;

                var cs = Mathf.Cos(theta);
                var sn = Mathf.Sin(theta);
                var px = b.velocity.x * cs - b.velocity.y * sn; 
                var py = b.velocity.x * sn + b.velocity.y * cs;
                b.velocity = new Vector2(px,py);
            }
            b.position = newPos;
            points[i] = b.position;
        }
        pointBuffer.SetData(points);
        worleyShader.SetBuffer(kernelHandle, "Points", pointBuffer);
    }

    private void OnDestroy()
    {
        pointBuffer.Release();
        pointBuffer.Dispose();
    }

    private void SetValues()
    {
        worleyShader.SetFloat("distMap", distMap);
        //worleyShader.SetFloat("dB", dB);
        //worleyShader.SetFloat("feed", feed);
        //worleyShader.SetFloat("k", k);
        //worleyShader.SetInt("centerY", sizeL/2);
        //worleyShader.SetInt("centerX",sizeL/2);
        //rdfShader.SetInt("iterations", iterationsPerPoint);
    }

    private void OnValidate() {
        if(EditorApplication.isPlaying)
            SetValues();
    }
}

public class Boingo
{
    public Vector2 position;
    public Vector2 velocity;
    public float speed;
}
