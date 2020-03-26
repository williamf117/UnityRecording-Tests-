using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UltimateReplay;
[ReplayIgnore]
public class ReplayPuppet : ReplayBehaviour
{
    [SerializeField]
    SkinnedMeshRenderer[] hands;
    [SerializeField]
    MeshRenderer Head;
    bool recording = false;
    bool replaying = false; 
    // Start is called before the first frame update
    void Start()
    {
       
    }

    // Update is called once per frame
    void Update()
    {
        try
        {
            //handle enabling and dissabling the replay puppet 
            if (recording == false && ReplayManager.IsRecording)
            {
                deacticate();
                recording = true;
            }
            else if (recording == true && !ReplayManager.IsRecording)
            {
                recording = false;
                Head.enabled = false;
            }

            if (replaying == false && ReplayManager.IsReplaying)
            {
                activate();
                replaying = true;
            }
            else if (replaying == true && !ReplayManager.IsReplaying)
            {
                replaying = false;
            }

            //defult to disabling the things
            if (ReplayManager.IsReplaying == false && ReplayManager.IsRecording == false)
            {
                deacticate();

            }
        }
        catch
        {
            Debug.Log("the replay manager is disposed");
        }
    }
    void activate()
    {
        foreach (SkinnedMeshRenderer mesh in hands)
        {
            Head.enabled = true;
            mesh.enabled = true;
        }
    }
    void deacticate()
    {
        Head.enabled = false;
        foreach (SkinnedMeshRenderer mesh in hands)
        {
            mesh.enabled = false;
        }
        
    }

}
