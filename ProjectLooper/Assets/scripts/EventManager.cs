using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EventManager : MonoBehaviour
{
    // the method to get access to the game manager methods and properties - ADDING STUFF
    public static EventManager Instance { get; private set; }

    //// making the constructor private so no other source and create it
    private EventManager() { }

    // will set the instance and then initialize the room
    private void Awake()
    {
        // if nothing in assigned to the instance property
        if (Instance == null)
        {
            Instance = new EventManager();
            DontDestroyOnLoad(gameObject);
        }
        else // if there is already an instance, it will destroy itself
        {
            Destroy(gameObject);
        }
    }



    //static List<Satellite> enterplanetInvoker=new List<Satellite>();
    //static List<UnityAction<string>> enterplanetListeners=new List<UnityAction<string>>();


    //public static void AddMissionCompleatInvokers(MissionBase invoker)
    //{
    //    MissionCompleateInvokers.Add(invoker);
    //    foreach(UnityAction<string> listener in MissionCompleateListeners)
    //    {
    //        invoker.AddMissionCompleteListener(listener);
    //    }
    //}

    //public static void AddMissionCompleteListeners(UnityAction<string> listener)
    //{
    //    MissionCompleateListeners.Add(listener);
    //    foreach(MissionBase invoker in MissionCompleateInvokers)
    //    {
    //        invoker.AddMissionCompleteListener(listener);
    //    }
    //}


}
