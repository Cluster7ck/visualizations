using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRotate : MonoBehaviour
{
    public float speed = 1;
    public float a = 0;
    public float r = 0;
    // Start is called before the first frame update
    void Start()
    {
        r = transform.position.z;
    }

    // Update is called once per frame
    void Update()
    {
        var x = Mathf.Sin(a) * r;
        var z = Mathf.Cos(a) * r;
        transform.position = new Vector3(x,0,z);
        transform.LookAt(Vector3.zero, Vector3.up);
        a += 1/(2 * Mathf.PI) * Time.deltaTime * speed;
    }
}
