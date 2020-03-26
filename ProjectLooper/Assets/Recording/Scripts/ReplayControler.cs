using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UltimateReplay;
using UltimateReplay.Storage;
using Valve.VR.InteractionSystem;
/// <summary>
/// handles plating and making new recordings 
/// </summary>
[ReplayIgnore]

public class ReplayControler : MonoBehaviour
{
    public static ReplayControler instance;
    [SerializeField]
    GameObject replayprefab, recordprefab;
    GameObject replayRig, recordRig ;
    //the audio source that the mic recording should play through
    [SerializeField]
    AudioSource AudioSource;
    
    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }
    }


    /// <summary>
    /// make a new recording 
    /// </summary>
    /// <param name="name"></param>
    public void createRecording(string name)
    {
        ReplayFileTarget target = ReplayManager.Target as ReplayFileTarget;
        // Set the location/name of the replay file to load
        target.FileOutputName = name+".replay";
        Debug.Log(name);
        //add it to the dictionary and save to a file. 
        GetComponent<micRecorder>().StartRecording(name);
        RecordingDictionaryClass.addRecording(name, name + ".replay", name + ".wav");
        RecordingDictionaryClass.SaveDictionary();

        ReplayManager.BeginRecording();
        Debug.Log("Recording");
    }

    /// <summary>
    /// stop the recording 
    /// </summary>
    public void StopRecording()
    {
        ReplayManager.StopRecording();
    }


    /// <summary>
    /// playback an old recording
    /// </summary>
    /// <param name="name"></param>
    public void SelectandStartReplay(string name)
    {
        //check for valid name 
        if (!RecordingDictionaryClass.RecordingDictionary.ContainsKey(name))
        {
            Debug.Log("name dose not exist");
            return;
        }

        string replayname;
        string replayAudio;
        string data = RecordingDictionaryClass.RecordingDictionary[name];
        string[] Data = data.Split(',');
        replayname = Data[0];
        replayAudio = Data[1];
        Debug.Log(replayAudio);
        AudioClip clip = Resources.Load<AudioClip>(name);
        Debug.Log(clip);
        AudioSource.clip = clip;
        // Get the active record target from the replay manager

        ReplayFileTarget target = ReplayManager.Target as ReplayFileTarget;



        // Set the location/name of the replay file to load

        if (!replayname.EndsWith ( ".replay"))
        {
            replayname += ".replay";
        }
        Debug.Log(name);
        target.FileOutputName = replayname;



        // Begin playback as normal and the file will be loaded

        ReplayManager.BeginPlayback();
        AudioSource.Play();
       
       // Instantiate(replayprefab);
    }

}
