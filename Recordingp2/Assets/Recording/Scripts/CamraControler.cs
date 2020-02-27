using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UltimateReplay;

/// <summary>
/// this class is responsable for enabling and disabling camras at the right time. 
/// </summary>
public class CamraControler : MonoBehaviour
{
    [SerializeField] Camera replay, recordingCam;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //set camra to the recording cam 
        if (ReplayManager.IsRecording)
        {
            recordingCam.enabled = true;
            replay.enabled = false;
            replay.gameObject.GetComponent<AudioListener>().enabled = false;
            recordingCam.gameObject.GetComponent<AudioListener>().enabled = true;

        }
        //set cam to replay cam 
        else if(ReplayManager.IsReplaying)
        {
            replay.gameObject.GetComponent<AudioListener>().enabled = true;
            recordingCam.gameObject.GetComponent<AudioListener>().enabled = false;
            replay.enabled = true;
            recordingCam.enabled = false;

        }
        //defualt to a recording cam
        else
        {
            replay.gameObject.GetComponent<AudioListener>().enabled = false;
            recordingCam.gameObject.GetComponent<AudioListener>().enabled = true;
            replay.enabled = false;
            recordingCam.enabled = true;
        }
    }
}
