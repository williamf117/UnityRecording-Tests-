using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UltimateReplay;
/// <summary>
/// This class records the mic input and uses the sawave script to save it as a wav file 
/// </summary>
public class micRecorder : MonoBehaviour
{
    AudioClip clip;
    bool recording = false;
    float M_Time = 0;
    string fileName;
   

    // Update is called once per frame
    void Update()
    {
        //add time to the time flote so the clip can be cut down 
        if (ReplayManager.IsRecording)
        {
            M_Time += UnityEngine.Time.deltaTime;
            recording = true;
        }
        else if(recording==true && !ReplayManager.IsRecording)
        {
            Stoprecording(fileName);
            recording = false;
        }
      
    }
    public void StartRecording(string name)
    {
        fileName = name;
        int minfrec;
        int maxfrec;
        //get the mic capibilitys 
        Microphone.GetDeviceCaps("", out minfrec, out maxfrec);
        //set the clip to hold the info the mic is giving 
        clip = Microphone.Start("", true, 300, maxfrec);
        //source.clip = clip;
        //could play back the clip in real time 
       
    }
    /// <summary>
    /// stop the recording and save it 
    /// </summary>
    public void Stoprecording(string filename)
    {
        Microphone.End("");
        M_Time /= 60;
        //clip=  SavWav.TrimSilence(clip, time);
        SavWav.Save(filename, clip);
    }


}
