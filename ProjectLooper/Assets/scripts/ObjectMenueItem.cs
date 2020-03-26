using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// this class is a container for all the info we need for the menue for each item 
/// </summary>
public class ObjectMenueItem : MonoBehaviour
{
    GameObject M_obj;
    string M_Name;
    Sprite M_image;
    TagsClass.Tags M_tag;

    public TagsClass.Tags Tag
    {
        get { return M_tag; }
        set { M_tag = value; }
    }

    public GameObject OBJ
    {
        get
        {
            return M_obj;
        }
        set { M_obj = value; }
    }
    public string Name
    {
        get { return M_Name; }
        set { M_Name = value; }
    }
    public Sprite Image
    {
        get { return M_image; }
        set { M_image = value; }
    }
    public ObjectMenueItem(GameObject obj, string name, Sprite image, TagsClass.Tags tag)
    {
        M_obj = obj;
        M_Name = name;
        M_image = image;
        M_tag = tag;
    }
}
