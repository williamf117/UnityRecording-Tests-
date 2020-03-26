using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// handle the menue scene enabling and hiding menue pannels
/// </summary>
public class MainMenueControler : MonoBehaviour
{
    [SerializeField]
    GameObject RecordOrPlay, RecordingName, SelectRecording;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    /// <summary>
    /// dissables the first pannel and enables the pannle allowing you to name a new recording 
    /// </summary>
    public void StartRecording()
    {
        RecordOrPlay.SetActive(false);
        RecordingName.SetActive(true);
    }
   /// <summary>
   /// disables first menue and enables second menue to select recording from list 
   /// </summary>
    public void StartPlayback()
    {
        RecordOrPlay.SetActive(false);
        SelectRecording.SetActive(true);
    }

}
