using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayMarchCamera : MonoBehaviour
{
    [SerializeField] private Renderer rend;

    // Start is called before the first frame update
    void Start()
    {
        //GetComponent<MeshRenderer>().material
    }

    // Update is called once per frame
    void Update()
    {
        float shininess = Mathf.PingPong(Time.time, 1.0f);
        rend.material.SetFloat("_Shininess", shininess);
    }
}
