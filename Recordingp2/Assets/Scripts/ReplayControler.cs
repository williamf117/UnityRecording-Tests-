using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UltimateReplay;
using UltimateReplay.Storage;
/// <summary>
/// handles plating and making new recordings 
/// </summary>
[ReplayIgnore]

public class ReplayControler : MonoBehaviour
{
    
    //the audio source that the mic recording should play through
    [SerializeField]
    AudioSource AudioSource;
    

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
        transform.parent.gameObject.GetComponent<micRecorder>().StartRecording(name);
        RecordingDictionaryClass.addRecording(name, name + ".replay", name + ".wav");
        RecordingDictionaryClass.SaveDictionary();

        ReplayManager.BeginRecording();
    }
    /// <summary>
    /// playback an old recording
    /// </summary>
    /// <param name="name"></param>
    public void SelectandStartReplay(string name)
    {
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
    }

}
