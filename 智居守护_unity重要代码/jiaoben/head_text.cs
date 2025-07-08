using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class HeadText : MonoBehaviour
{
    public Text Data;
    public Text Time;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Data.text = DateTime.Now.ToString("yyyy-MM-dd");
        Time.text = DateTime.Now.ToString("HH:mm:ss");
    }
}