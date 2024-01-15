using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public float speed = 10.0f;
    private Camera _camera;

    private void Start()
    {
        this._camera = GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            // Move camera forward
            transform.Translate(new Vector3(0, 1f, 0f) * Time.deltaTime * speed);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            // Move camera backward
            transform.Translate(new Vector3(0, -1f, 0f) * Time.deltaTime * speed);
        }
        if (Input.GetKey(KeyCode.A))
        {
            // Move camera up
            transform.Translate(new Vector3(-1f, 0, 0f) * Time.deltaTime * speed);
        }
        else if (Input.GetKey(KeyCode.D))
        {
            // Move camera down
            transform.Translate(new Vector3(1f, 0f, 0f) * Time.deltaTime * speed);
        }
        if (Input.GetKey(KeyCode.E))
        {
            _camera.orthographicSize += 1f * Time.deltaTime * speed/2f;
        }
        else if (Input.GetKey(KeyCode.Q))
        {
            _camera.orthographicSize -= 1f * Time.deltaTime * speed/2f;
        }
    }
}
