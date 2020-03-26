using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ObjectDictionary : MonoBehaviour
{
    [SerializeField]
    GameObject[] objects;
    [SerializeField]
    Sprite[] images;
  static  Dictionary<string, ObjectMenueItem> Placables;

    // Start is called before the first frame update
    void Start()
    {
      ObjectDictionary.Placables = new Dictionary<string, ObjectMenueItem>();
        init();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void init()
    {
        foreach(GameObject go in objects)
        {
            string tempname = go.name;
            ObjectMenueItem temp = go.GetComponent<ObjectMenueItem>();
            Placables.Add(tempname, temp);
        }
    }
    
    

}
