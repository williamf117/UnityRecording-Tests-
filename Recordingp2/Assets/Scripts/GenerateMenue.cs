using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// generates a menue alloing user to select a recording to watch.
/// </summary>
public class GenerateMenue : MonoBehaviour
{
    [SerializeField]
    GameObject Button;



    // Start is called before the first frame update
    void Start()
    {
        Build();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    /// <summary>
    /// build the menue for selecting from saved recordings 
    /// </summary>
    public void Build()
    {
        //refrence to the dictionary data structure 
        if (RecordingDictionaryClass.RecordingDictionary == null)
        {
            RecordingDictionaryClass.Init();
            //RecordingDictionaryClass.load();
        }
        var dic = RecordingDictionaryClass.RecordingDictionary;
        int offset = -160;
        //for each entery create a button set its text to the key and its on press event 
        foreach(KeyValuePair<string,string> s in dic)
        {
            GameObject b= Instantiate(Button, gameObject.transform);
            b.transform.position = new Vector3(transform.position.x, 130 - offset, transform.position.z);
            offset -= 40;
            b.GetComponent<Button>().onClick.AddListener(delegate { buttonPress(s.Key); });
            b.transform.GetChild(0).gameObject.GetComponent<Text>().text = s.Key;
        }

    }
    public void buttonPress(string key)
    {
        //load the main scene and set replay target file to the file name
        GameManager.Instance.playRecording(key);
    }

}
