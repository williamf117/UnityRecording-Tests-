using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ModleMenueItem : MonoBehaviour
{
    public GameObject M_obj;
    public string M_name;
    public Image M_sprite;

  public  ModleMenueItem(GameObject GO, string name, Image Sprite)
    {
        this.M_obj = GO;
        this.M_name = name;
        this.M_sprite = Sprite;
    }

}
