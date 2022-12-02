using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KaleidoscopeController : MonoBehaviour
{
    [SerializeField] private Renderer target;
    [SerializeField] private float replicate = 4.0f;
    [SerializeField] private float zoom = 1.0f;

    // Update is called once per frame
    void Update()
    {
        target.material.SetFloat("_Zoom", Mathf.Sin(Time.deltaTime * 0.1f) + 2);
    }
}
