using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Menue : MonoBehaviour
{
    [SerializeField]
    string filename;
    ReplayControler controler;
    // Start is called before the first frame update
    void Start()
    {
        controler= ReplayControler.instance;
        RecordingDictionaryClass.Init();
    }
    public void StartRecording()
    {
        controler.createRecording(filename);
    }
    public void StopRecording()
    {
        controler.StopRecording();
    }
    public void Playback()
    {
        controler.SelectandStartReplay(filename);
    }

}
