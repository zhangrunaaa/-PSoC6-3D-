using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAt : MonoBehaviour
{
    public GameObject MainCamera;
    public GameObject CameraL;

    // Start is called before the first frame update
    private void Awake()
    {
        MainCamera = GameObject.Find("Main Camera");
    }

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        gameObject.transform.LookAt(MainCamera.transform.position);
    }

    private void OnMouseDown()
    {
        MainCamera.SendMessage("OnMouseMove", CameraL.transform);
    }
}