using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public  class modleLibrary:MonoBehaviour
{
    [SerializeField]
    GameObject[] objects;
    [SerializeField]
     string[] names;
    [SerializeField]
     Image[] sprites;
    public static List<ModleMenueItem> modles;

    private void Awake()
    {
        init();
    }

    private  void init()
    {
        for(int i=0; i<= objects.Length; i++)
        {
            if (objects[i] != null)
            {
                modles.Add(new ModleMenueItem(objects[i], names[i], sprites[i]));
            }
        }
    }



}
