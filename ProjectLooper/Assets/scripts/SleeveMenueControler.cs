using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SleeveMenueControler : MonoBehaviour
{
    [SerializeField]
    GameObject up, down;
    bool spining = false;
    Vector3 targetrotaion;

    private void Awake()
    {

    }

    // Start is called before the first frame update
    void Start()
    {
        //RotUp();
        targetrotaion = Vector3.zero;
    }

    // Update is called once per frame
    void Update()
    {
        transform.localEulerAngles = Vector3.RotateTowards(transform.localEulerAngles, targetrotaion, 1, 10);
        Debug.Log(targetrotaion);
    }
    public void RotUp()
    {
        //  transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, 90, 0), 5 * Time.deltaTime);
        if (!spining)
        {
            float target = transform.eulerAngles.z + 90; 
            targetrotaion = new Vector3(0, 0, target);
            // StartCoroutine(ContinuousRotation(90));
            // spining = true;
        }
    }
    public void RotDown()
    {
        if (!spining)
        {
            float target = transform.eulerAngles.z - 90;
            targetrotaion = new Vector3(0, 0, target);
            // spining = true;
            // StartCoroutine(ContinuousRotation(-90));
        }

    }
}

