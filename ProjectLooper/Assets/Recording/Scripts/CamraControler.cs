using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UltimateReplay;

/// <summary>
/// this class is responsable for enabling and disabling camras at the right time. 
/// </summary>
public class CamraControler : MonoBehaviour
{
    [SerializeField] 
  public GameObject replay,recordingobj;
 
   [SerializeField] 
    Camera recordingCam;

    public enum state  {
        Recording,
        watching,
        idle,
    }
    state s = state.idle;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        //set camra to the recording cam 
        if (ReplayManager.IsRecording &&s!=state.Recording)
        {
            s = state.Recording;
            recordingCam.enabled=true;
          
            //replay.gameObject.GetComponent<AudioListener>().enabled = false;
            //recordingCam.gameObject.GetComponent<AudioListener>().enabled = true;

        }
        //set cam to replay cam 
        else if(ReplayManager.IsReplaying&& s != state.watching)
        {
            
            s = state.watching;
            //replay.gameObject.GetComponent<AudioListener>().enabled = true;
            //recordingCam.gameObject.GetComponent<AudioListener>().enabled = false;
            replay.SetActive(true);
            recordingCam.enabled = false;
            

        }
        //defualt to a recording cam
        else 
        {
            if (s != state.idle&& !ReplayManager.IsReplaying&&! ReplayManager.IsRecording)
            {
               
                s = state.idle;
                // replay.gameObject.GetComponent<AudioListener>().enabled = false;
                //recordingCam.gameObject.GetComponent<AudioListener>().enabled = true;
                
                replay.SetActive(false);
                recordingCam.enabled = true;
            }
          
        }
    }
}
