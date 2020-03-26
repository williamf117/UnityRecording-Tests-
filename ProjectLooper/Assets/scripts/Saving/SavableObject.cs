using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SavableObject : MonoBehaviour
{

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public string save()
    {

        return (gameObject.name + "," + transform.position.ToString() + "," + transform.rotation.ToString());
    }
}
