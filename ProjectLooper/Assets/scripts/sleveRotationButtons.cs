using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class sleveRotationButtons : MonoBehaviour
{
    public bool up;
    [SerializeField]
    SleeveMenueControler controler;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "hands")
        {
            Debug.Log("hands");
            if (up)
            {
                controler.RotUp();

            }
            else
            {
                controler.RotDown();
            }
        }
    }
}
