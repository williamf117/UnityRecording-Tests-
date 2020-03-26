using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Menue : MonoBehaviour
{
    ReplayControler controler;
    // Start is called before the first frame update
    void Start()
    {
        controler= ReplayControler.instance;
        RecordingDictionaryClass.Init();
    }
    public void StartRecording()
    {
        controler.createRecording("TestRecording");
    }
    public void StopRecording()
    {
        controler.StopRecording();
    }
    public void Playback()
    {
        controler.SelectandStartReplay("TestRecording");
    }

}
