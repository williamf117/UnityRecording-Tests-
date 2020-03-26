using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ButtonCollorTint : MonoBehaviour
{
    Button button;
    // Start is called before the first frame update
    void Start()
    {
        button = GetComponent<Button>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
         button.Select();

       // button.onClick.Invoke();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(37, 88, 255, 255);
        colors.highlightedColor = new Color32(37, 88, 255, 255);
        button.colors = colors;
    }
    private void OnCollisionExit(Collision collision)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(255, 255, 255, 255);
        //colors.highlightedColor = new Color32(146, 171, 250, 255);
        button.colors = colors;
    }
    private void OnTriggerEnter(Collider other)
    {
        //button.Select();

        // button.onClick.Invoke();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(37, 88, 255, 255);
        colors.highlightedColor = new Color32(37, 88, 255, 255);
        button.colors = colors;
    }
    private void OnTriggerExit(Collider other)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = new Color32(255, 255, 255, 255);
        //colors.highlightedColor = new Color32(146, 171, 250, 255);
        button.colors = colors;
    }

}
