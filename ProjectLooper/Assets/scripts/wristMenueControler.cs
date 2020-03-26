using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR.InteractionSystem;
using UltimateReplay;

public class wristMenueControler : MonoBehaviour
{
    [SerializeField]
    GameObject Parent;
    [SerializeField]
    Sprite Recording, playback, idle;
    [SerializeField]
    Image RecordingState;
    // Start is called before the first frame update
    void Start()
    {
        //StartCoroutine(Rotate(Vector3.forward, -90, .5f));
        transform.localRotation = Quaternion.identity;
    }

    // Update is called once per frame
    void Update()
    {
        //check if we are recording or in play back
        if (ReplayManager.IsRecording)
        {
            RecordingState.sprite = Recording;
        }
        else if (ReplayManager.IsReplaying)
        {
            RecordingState.sprite = playback;
        }
        else
        {
            RecordingState.sprite = idle;
        }
    }
    private void LateUpdate()
    {
        //transform.localEulerAngles = new Vector3(0, 0, transform.eulerAngles.z);
    }

    public void Scrollup()
    {
       StartCoroutine( Rotate(Vector3.forward, 90, .1f));
    }

    public void ScrollDown()
    {
       
        StartCoroutine( Rotate(Vector3.forward, -90, .1f));
    }

    IEnumerator Rotate(Vector3 axis, float angle, float duration = 1.0f)
    {
     //   detach();
        Quaternion from = transform.rotation;
        Quaternion to = transform.rotation;
        to *= Quaternion.Euler(axis * angle);

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;

            //zero every ting but the z
          //  transform.localEulerAngles = new Vector3(0, 0, transform.eulerAngles.z);

            yield return null;
        }
        transform.rotation = to;
       // attach();
    }
    void detach()
    {
        transform.parent = null;
    }
    void attach()
    {
        transform.parent = Parent.transform;
    }
    private void OnTriggerEnter(Collider other)
    {
        
    }
}
