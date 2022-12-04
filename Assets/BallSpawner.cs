using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    public GameObject ball;
    float ellapsedTIme = 0;
    public float spawnTime = 1;
    public ReactionDiffusion reactionDiffusion;
    private List<Rigidbody> bodies = new List<Rigidbody>();
    public List<MeshRenderer> renderers = new List<MeshRenderer>();

    public float bounce = 5;
    // Start is called before the first frame update
    void Start()
    {
        // TODO
        // CHANGE SHAPE ON SNARE
        // CHANGE SIZE ON HAT? JITTERYNESS?? COLOR??
    }

    // Update is called once per frame
    void Update()
    {
        if(ellapsedTIme > spawnTime) {
            var newBall = GameObject.Instantiate(ball);
            newBall.transform.position = new Vector3(Random.Range(-8.0f, 8.0f), 4, Random.Range(-10.0f, 10.0f));
            //newBall.GetComponent<MeshRenderer>().material.mainTexture = reactionDiffusion.kaleidoscopeTexture;
            bodies.Add(newBall.GetComponent<Rigidbody>());
            ellapsedTIme = 0;
        }    
        ellapsedTIme += Time.deltaTime;

        if(Input.GetKeyDown(KeyCode.Space))
        {
            foreach(var renderer in renderers)
            {
                renderer.material = ball.GetComponent<MeshRenderer>().material;
            }

            foreach (var item in bodies)
            {
                var planeDelt = new Vector3(Random.Range(-0.2f,0.2f),0,Random.Range(-0.24f,0.24f));
                item.AddForce((planeDelt+Vector3.up)* bounce, ForceMode.Impulse);
            }
        }
    }
}
