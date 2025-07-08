using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class camera_move : MonoBehaviour
{
    public float SpeedR = 1.5f;
    public float SpeedM = 5f;
    public float SpeedA = 1f;
    private float z = 0f;
    public float MoveSpeed = 10.0f;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Move();
    }
    public void Move()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            SpeedA = 3.0f;
        }
        else
        {
            SpeedA = 1.0f;
        }

        if (Input.GetMouseButton(1))
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            transform.Rotate(Vector3.up, Input.GetAxis("Mouse X") * SpeedR, Space.World);
            transform.Rotate(Vector3.left, Input.GetAxis("Mouse Y") * SpeedR, Space.Self);

            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            transform.Translate(new Vector3(h, 0, v) * SpeedM * SpeedA * Time.deltaTime);
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
    public void OnMouseMove(Transform CT)
    {
        StartCoroutine(MoveCamera(CT));
    }

    IEnumerator MoveCamera(Transform CT)
    {
        while (Vector3.Distance(transform.position, CT.transform.position) > 0.1f
              && Quaternion.Angle(transform.rotation, CT.transform.rotation) > 5.0f)
        {
            transform.position = Vector3.Lerp(transform.position, CT.transform.position,
                                               MoveSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, CT.transform.rotation,
                                                  MoveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = CT.transform.position;
        transform.rotation = CT.transform.rotation;
    }








}
