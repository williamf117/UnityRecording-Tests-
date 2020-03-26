using System;
using System.Collections.Generic;
using UnityEngine;
using UltimateReplay.Core;
using UltimateReplay.Storage;
using UltimateReplay.Util;

#if UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif

namespace UltimateReplay
{
    /// <summary>
    /// Represents a playback node that can be used to calcualte playback offsets.
    /// </summary>
    public enum PlaybackOrigin
    {
        /// <summary>
        /// The start of the playback sequence.
        /// </summary>
        Start,
        /// <summary>
        /// The current frame in the playback sequence.
        /// </summary>
        Current,
        /// <summary>
        /// The end of the playback sequence.
        /// </summary>
        End,
    }

    /// <summary>
    /// The playback direction used during replay plaback.
    /// </summary>
    public enum PlaybackDirection
    {
        /// <summary>
        /// The replay should be played back in normal mode.
        /// </summary>
        Forward,
        /// <summary>
        /// The replay should be played back in reverse mode.
        /// </summary>
        Backward,
    }

    public enum PlaybackEndBehaviour
    {
        EndPlayback,
        StopPlayback,
        LoopPlayback
    }

    /// <summary>
    /// The state of the active <see cref="ReplayManager"/>. 
    /// </summary>
    public enum PlaybackState
    {
        /// <summary>
        /// The manager is doing nothing.
        /// </summary>
        Idle = 0,
        /// <summary>
        /// The manager is currently recording the scene using the current record settings.
        /// </summary>
        Recording,
        /// <summary>
        /// The manager is currently paused but is expecting to resume recording.
        /// </summary>
        Recording_Paused,
        Recording_Paused_Playback,
        /// <summary>
        /// The manager is performing playback using the current settings.
        /// </summary>
        Playback,
        /// <summary>
        /// The manager is currently paused but is expecting to resume playback.
        /// </summary>
        Playback_Paused,
    }

    /// <summary>
    /// The update method used by the replay manager for all recording and replaying samples.
    /// </summary>
    public enum UpdateMethod
    {
        /// <summary>
        /// Use the Update method.
        /// </summary>
        Update,
        /// <summary>
        /// Use the late update method.
        /// </summary>
        LateUpdate,
        /// <summary>
        /// Use the fixed update method.
        /// </summary>
        FixedUpdate,
    }

    /// <summary>
    /// The main interface for Ultimate Replay and allows full control over object recording and playback.
    /// </summary>
    public sealed class ReplayManager : MonoBehaviour
    {
        // Events
        /// <summary>
        /// Called by the replay system whenever it needs to instantiate a prefab for use during playback.
        /// You can add a listener to override the default behaviour which can be useful if you want to handle the instantiation manually for purposes such as object pooling.
        /// </summary>
        public static Func<GameObject, Vector3, Quaternion, GameObject> OnReplayInstantiate;

        /// <summary>
        /// Called by the replay system whenever it needs to destroy a game object in order to restore a previous scene state.
        /// You can add a listener to override the default behaviour which can be useful if you want to handle the destruction manually for purposes such as object pooling.
        /// </summary>
        public static Action<GameObject> OnReplayDestroy;


        // Private
        private static IReplayPreparer preparer = new DefaultReplayPreparer();
        private static ReplayManager instance = null;
        private static bool isSceneDisposing = false;
        private static bool isDisposing = false;

        private ReplayScene scene = new ReplayScene();
        private ReplaySequencer sequence = new ReplaySequencer();
        private ReplayTarget target = null;
        private PlaybackState state = PlaybackState.Idle;
        private bool singlePlayback = false;

        private ReplayTimer recordTimer = new ReplayTimer();
        private ReplayTimer recordStepTimer = new ReplayTimer();    // Ticks every time the target fps has been reached

        private List<ReplayObject> replayPrefabs = new List<ReplayObject>();

        // Public
        /// <summary>
        /// When true, the manager will automatically begin recording the scene one it is initialized.
        /// </summary>
        [Tooltip("When true, the manager will automatically start recording on startup")]
        public bool recordOnStart = true;

        [Tooltip("When true, the game object will survie scene changes. This is useful if you dont want to place a replay manager in every scene")]
        public bool dontDestroyOnLoad = false;

        /// <summary>
        /// What action should be taken when playback has completed.
        /// By default the replay system will end playback and switch back to live mode.
        /// </summary>
        [Tooltip("The beaviour used when playback reaches the end of the current replay")]
        public PlaybackEndBehaviour playbackEndBehaviour = PlaybackEndBehaviour.EndPlayback;

        /// <summary>
        /// Which update method should be used by the replay manager.
        /// For best compatibility with game scritps or 3rd party assets you may need to change this value so that sample recording occurs at a different time in the loop cycle.
        /// </summary>
        [Tooltip("The update method used to record and replay. You may need to change this value for best compatibility with game scripts or 3rd party assets")]
        public UpdateMethod updateMethod = UpdateMethod.Update;        

        /// <summary>
        /// The target record framerate of the sampler. The higher this value the higher the memory consumption and cpu usage will be.
        /// You will need to fine tune this value to tradeoff performance or memory usage for replay accuracy.
        /// </summary>
        [Range(1, 48)]
        [Tooltip("The target frame rate to record at. Higher values provide more accurate playback but will result in more cpu load and memory usage")]
        public int recordFPS = 8;

        /// <summary>
        /// A collection of prefabs that may be spawned or destroyed during recording and as a result amy need to be spawned or destroyed in order to accuratley recreate the replay.
        /// </summary>
        public GameObject[] prefabs = new GameObject[0];


        // Properties
        /// <summary>
        /// Get the active replay manager in the scene.
        /// If no replay manager could be found then one will be created with default settings.
        /// This property may be null in a rare case where the active replay manager was destroyed in the same frame as an application quit event was issued. In this situation the replay manager cannot be recreated as it would cause leaked objects.
        /// </summary>
        public static ReplayManager Instance
        {
            get
            {
#if ULTIMATEREPLAY_TRIAL == true
                if (Application.isEditor == false)
                    throw new ApplicationException("Ultimate Replay trial version cannot be used in standalone player");
#endif

                // Check for disposing
                if(instance == null)
                    if (isDisposing == true)
                        throw new ObjectDisposedException("The replay manager instance has been disposed and will not be recreated because the game is quitting. Use 'ReplayManager.IsDisposing' to check if you should access the replay manager");

                if (instance == null)
                {
                    // Find all managers
                    ReplayManager[] managers = Component.FindObjectsOfType<ReplayManager>();

                    // Check for any managers
                    if(managers.Length > 0)
                    {
                        // Display a multiple manager warning
                        if (managers.Length > 1)
                            Debug.LogWarning("There are multiple replay managers in the scene. " + managers[0].name + " will become the active replay manager instance");
                        
                        // Get the first manager
                        instance = managers[0];
                    }
                    else
                    {
                        // Create a new instance
                        GameObject go = new GameObject(typeof(ReplayManager).Name);

                        // Add the component
                        instance = go.AddComponent<ReplayManager>();                      
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// Returns true if the singleton is currently disposing.
        /// This will only occur when the game is about to quit. 
        /// You can use this property in 'OnDestroy' to determine whether you should access the replay amnager instance.
        /// </summary>
        public static bool IsDisposing
        {
            get { return isDisposing == true || isSceneDisposing == true; }
        }

        /// <summary>
        /// Access the current <see cref="IReplayPreparer"/> that the active replay manager will use to prepare game objects for replay. 
        /// By default a <see cref="DefaultReplayPreparer"/> is used. 
        /// </summary>
        public static IReplayPreparer Preparer
        {
            get { return preparer; }
            set
            {
                // Dont allow null assignment
                if (value == null)
                    value = new DefaultReplayPreparer();

                // Assign the preparer
                preparer = value;
            }
        }

        /// <summary>
        /// The current replay target that is being used to store the replay data.
        /// By default, the replay target is <see cref="ReplayMemoryTarget"/>.
        /// </summary>
        public static ReplayTarget Target
        {
            get
            {
                // Check for a replay manager
                if (Instance == null)
                    return null;

                // If we dont have a target then create one
                if (Instance.target == null)
                {
                    // Try to get an existing target
                    Instance.target = Instance.GetComponent<ReplayTarget>();

                    // Create a default memory target
                    if (Instance.target == null)
                        Instance.target = Instance.gameObject.AddComponent<ReplayMemoryTarget>();

                    // Create the sequencer because the target has changed
                    Instance.sequence.Target = Instance.target;
                }

                return Instance.target;
            }
            set
            {
                // Check for a replay manager
                if (Instance == null)
                    return;

                // Dont allow null targets
                if (value != null)
                {
                    // Store our new replay target
                    Instance.target = value;

                    // Create our replay sequence
                    Instance.sequence.Target = Instance.target;
                }
            }
        }

        /// <summary>
        /// Get the <see cref="ReplayScene"/> associated with the replay system.
        /// </summary>
        public static ReplayScene Scene
        {
            get
            {
                // Check for null replay instance
                if (Instance == null)
                    return null;

                // Get the replay scene
                return Instance.scene;
            }
        }

        /// <summary>
        /// Returns true if the manager is currently recording the scene.
        /// Note: If recording is paused this value will still be true.
        /// </summary>
        public static bool IsRecording
        {
            get
            {
                // Make sure we have a manager
                if (Instance == null)
                    return false;

                return Instance.state == PlaybackState.Recording ||
                    Instance.state == PlaybackState.Recording_Paused;
            }
        }

        /// <summary>
        /// Returns true if the manager is currently playing back previously recorded replay data.
        /// Note: if playback is paused this value will still be true.
        /// </summary>
        public static bool IsReplaying
        {
            get
            {
                // Make sure we have a manager
                if (Instance == null)
                    return false;

                // Check for any playback
                return Instance.state == PlaybackState.Playback ||
                    Instance.state == PlaybackState.Playback_Paused;
            }
        }

        /// <summary>
        /// Returns true when the active replay manager is in any paused state.
        /// Paused states could include <see cref="PlaybackState.Playback_Paused"/> or <see cref="PlaybackState.Recording_Paused"/>.  
        /// </summary>
        public static bool IsPaused
        {
            get
            {
                // Make sure we have a manager
                if (Instance == null)
                    return false;

                // Check for any paused state
                return Instance.state == PlaybackState.Playback_Paused || 
                    Instance.state == PlaybackState.Recording_Paused;
            }
        }

        /// <summary>
        /// Gets the current <see cref="PlaybackDirection"/> of replay playback.
        /// </summary>
        public static PlaybackDirection PlaybackDirection
        {
            get { return ReplayTime.TimeScaleDirection; }
        }

        /// <summary>
        /// Get the current playback time in seconds.
        /// This value will never be greater than <see cref="ReplayTarget.duration"/>. 
        /// </summary>
        public static float CurrentPlaybackTime
        {
            get
            {
                // Make sure we have a manager
                if (Instance == null)
                    return 0;

                // Get sequence time#
                return Instance.sequence.CurrentTime;
            }
        }

        /// <summary>
        /// Get the current playback time as a normalized value between 0-1.
        /// 0 represents the starting frame of the recording and 1 represents the very last frame of the recording.
        /// </summary>
        public static float CurrentPlaybackTimeNormalized
        {
            get
            {
                // Make sure we have a manager
                if (Instance == null)
                    return 0;

                // Calculate the normalized progress
                return Instance.sequence.CurrentTimeNormalized;
            }
        }

        // Methods
        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void Awake()
        {
            isSceneDisposing = false;

            // Make the game object persistent
            if (dontDestroyOnLoad == true)
                DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Called by Unity.
        /// Allows the active replay manager to initialize.
        /// </summary>
        public void Start()
        {
            // Register replay prefabs
            foreach (GameObject go in prefabs)
                RegisterReplayPrefab(go);

            // Make sure we have a replay target - this will cause a replay target to be found or created
            if (Target == null) { }

            // Start recording
            if (recordOnStart == true)
                BeginRecording();

            // Create our timers
            recordStepTimer.Interval = (1.0f / recordFPS);

#if UNITY_5_4_OR_NEWER
            SceneManager.sceneLoaded += (Scene scene, LoadSceneMode mode) =>
            {
                // Trigger the scene change event
                OnActiveSceneChanged();
            };
#endif
            
#if ULTIMATEREPLAY_TRIAL == true
            // Use editor library so that the reference is not compile-optimized out
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

#if UNITY_5_4_OR_NEWER == false
        /// <summary>
        /// Called by Unity.
        /// Allows the active replay manager to cleanup recordings when a scene change is made.
        /// </summary>
        /// <param name="index">The level id</param>
        public void OnLevelWasLoaded(int index)
        {
            // Trigger the scene change event
            OnActiveSceneChanged();
        }
#endif

        /// <summary>
        /// Called by Unity.
        /// Allows the singleton to prevent recreation of the instance when the game is about to quit.
        /// </summary>
        public void OnApplicationQuit()
        {
            // Set the disposing flag
            isDisposing = true;
        }
        
        /// <summary>
        /// Called by Unity.
        /// Allows the active replay manager to update recoring or playback.
        /// </summary>
        public void Update()
        {
            if (Time.timeScale == 0)
            {
                Time.timeScale = 1;
            }
            if (updateMethod == UpdateMethod.Update)
                UpdateState(Time.deltaTime);
        }

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void LateUpdate()
        {
            if (updateMethod == UpdateMethod.LateUpdate)
                UpdateState(Time.deltaTime);
        }

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void FixedUpdate()
        {
            if (updateMethod == UpdateMethod.FixedUpdate)
                UpdateState(Time.fixedDeltaTime);
        }

        /// <summary>
        /// The main update method of the replay manager. 
        /// Causes the replay manager to update its state based on the current <see cref="state"/>. 
        /// </summary>
        /// <param name="deltaTime"></param>
        public void UpdateState(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                Debug.Log(Time.timeScale);
                throw new InvalidOperationException("Delta time value must be greater than '0'");
            }

            switch(state)
            {
                case PlaybackState.Idle: break;
                case PlaybackState.Playback_Paused: break; // Do nothing
                case PlaybackState.Recording_Paused: break; // Do nothing

                case PlaybackState.Recording_Paused_Playback:
                case PlaybackState.Playback:
                    {
                        // Make sure we are not in single frame mode
                        if (singlePlayback == false)
                        {
                            ReplaySnapshot frame;

                            // Update the sequencer
                            ReplaySequenceResult result = sequence.UpdatePlayback(out frame, playbackEndBehaviour, deltaTime);
                            
                            // Check if the sequence has a new frame to be displayed
                            if (result == ReplaySequenceResult.SequenceAdvance)
                            {
                                // Restore the scene state
                                scene.RestoreSnapshot(frame, Target.InitialStateBuffer);                                
                            }
                            // Check if the sequence is at the end of the recording
                            else if(result == ReplaySequenceResult.SequenceEnd)
                            {
                                StopPlayback();
                            }

                            // Update the playback
                            if (result == ReplaySequenceResult.SequenceIdle ||
                                result == ReplaySequenceResult.SequenceAdvance)
                            {
                                // This calls 'updateReplay' on all replay obejcts in the scene
                                ReplayBehaviour.Events.CallReplayUpdateEvents();
                            }
                        }
                        
                    } break;
                    
                case PlaybackState.Recording:
                    {
                        // Update the replay timer - Pass the delta time instead of using Time.deltaTime
                        ReplayTimer.Tick(deltaTime);

                        // Make sure enough time has passed to record a sample
                        if (recordStepTimer.HasElapsed() == true)
                        {
                            // Get the scene state
                            ReplaySnapshot currentState = scene.RecordSnapshot(Instance.recordTimer.ElapsedSeconds, Target.InitialStateBuffer);

                            // Write it to the replay target
                            Target.RecordSnapshot(currentState);
                        }
                    } break;
            }
        }

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void OnValidate()
        {
            // Allow inspector values to be modified at runtime
            recordStepTimer.Interval = (1.0f / recordFPS);

            // Update prefab links
            if (Application.isPlaying == false)
            {
                // Check for valid array
                if (prefabs != null)
                {
                    // Process all prefabs
                    foreach (GameObject go in prefabs)
                    {
                        if (go != null)
                        {
                            // Find the object component
                            ReplayObject obj = go.GetComponent<ReplayObject>();

                            // Make sure the prefab link is updated
                            if (obj != null)
                                obj.UpdatePrefabLinks();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Called by Unity.
        /// Allows the active replay manager to cleanup any active recordings.
        /// </summary>
        public void OnDestroy()
        {
            // Check for recording
            if (IsRecording == true)
                StopRecording();

            if (target != null)
            {
                // Commit target
                //target.PrepareTarget(ReplayTargetTask.Commit);

                // Destroy the target also
                Destroy(target);
            }

            // Check for scene disposal
            if (dontDestroyOnLoad == false)
                isSceneDisposing = true;
        }

        /// <summary>
        /// Use this method to begin sampling the recorded objects in the scene.
        /// If <see cref="recordOnStart"/> is true then this method will be called automatically when the manager is initialized. 
        /// Any state information will be recored via the assigned <see cref="ReplayTarget"/> (Default <see cref="ReplayMemoryTarget"/>).  
        /// <param name="cleanRecording">When true, any previous recording data will be discarded</param>
        /// </summary>
        public static void BeginRecording(bool cleanRecording = true)
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Throw away any leftover recording data
            if (cleanRecording == true)
                DiscardRecording();

            // Check if we are replaying
            if (IsReplaying == true)
                StopPlayback();

            // Inform the replay target that we are about to begin recording
            Target.PrepareTarget(ReplayTargetTask.PrepareWrite);
            
                

            if (instance.state != PlaybackState.Recording_Paused)
            {
                // Reset the record timer
                Instance.recordStepTimer.Reset();
                Instance.recordTimer.Reset();


                // Every recording must begin with a frame that has a time stamp of 0
                ReplaySnapshot currentState = Instance.scene.RecordSnapshot(0, Target.InitialStateBuffer);

                // Write it to the replay target
                Target.RecordSnapshot(currentState);
            }

            // Change to recording state
            Instance.state = PlaybackState.Recording;
        }

        /// <summary>
        /// Use this method when you want to pause recording but may continue recording at any point.
        /// A good candidate for pausing recording is when the user pauses the game and is shown a pause menu.
        /// The manager must already be recording otherwise this method will have no effect.
        /// </summary>
        public static void PauseRecording()
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Make sure we are recording before pausing
            if (Instance.state == PlaybackState.Recording)
            {
                //StopRecording();
                Instance.state = PlaybackState.Recording_Paused;
                StopRecording();
            }
        }

        /// <summary>
        /// Use this method to resume recording after a previous call to <see cref="PauseRecording"/>.
        /// The manager must already be recording otherwise this method will have no effect.
        /// </summary>
        public static void ResumeRecording()
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Change state only if replay is currently paused
            if (Instance.state == PlaybackState.Recording_Paused)
            {
                instance.state = PlaybackState.Recording_Paused;
                BeginRecording(false);
            }
            //Instance.state = PlaybackState.Recording;

        }

        /// <summary>
        /// Use this method to stop recording after a previous call to <see cref="BeginRecording"/>.
        /// The manager must already be recording otherwise this method will have no effect.
        /// This method must be called before attempting to playback otherwise you may get unpredicatable results.
        /// </summary>
        public static void StopRecording()
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Change state if replay is currently recording
            if (IsRecording == true)
            {
                

                // Finalize the recording target
                Instance.target.PrepareTarget(ReplayTargetTask.Commit);

                // Inform the replay target that we are about to stop recording
                // We should automatically switch to read mode and prepare for any calls to 'Seek' or 'BeginPlayback'
                Instance.target.PrepareTarget(ReplayTargetTask.PrepareRead);

                if (instance.state != PlaybackState.Recording_Paused)
                {
                    // Stop timer
                    Instance.recordTimer.Reset();

                    // Change to idle state
                    Instance.state = PlaybackState.Idle;
                }
            }
        }

        /// <summary>
        /// This method will throw away any recorded data and flush the replay target if necessary.
        /// This method can be called at any time.
        /// If the manager is currently recording then all previous data will be discarded and recording will continue.
        /// If the manager is currently replaying then all replay data will be discarded and playback will stop.
        /// </summary>
        public static void DiscardRecording()
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Call discard on the target
            if(Instance.target != null)
                Instance.target.PrepareTarget(ReplayTargetTask.Discard);

            // Check if we should stop playback
            if(IsReplaying == true)
            {
                // Stop playback because there is no data.
                StopPlayback();
            }
        }

        /// <summary>
        /// Use this method to specify where in the replay sequence the playback should start. 
        /// If the offset does not lie within the bounds of the replay then the value will be clamped to represent either the start or end frame.
        /// </summary>
        /// <param name="offset">The amount of time in seconds to offset the playback</param>
        /// <param name="origin">The playback node to take the offset from. If <see cref="PlaybackOrigin.End"/> is specified then the offset value will be used as a negative offset</param>
        public static void SetPlaybackFrame(float offset, PlaybackOrigin origin = PlaybackOrigin.Start)
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Move our current sequence frame
            ReplaySnapshot frame = Instance.sequence.SeekPlayback(offset, origin, false);

            // Check for a valid frame
            if (frame != null)
            {
                // Restore the scene state
                Instance.scene.RestoreSnapshot(frame, Target.InitialStateBuffer);

                // Call the reset event
                ReplayBehaviour.Events.CallReplayResetEvents();

                // This calls 'updateReplay' on all replay obejcts in the scene
                ReplayBehaviour.Events.CallReplayUpdateEvents();
            }
        }

        /// <summary>
        /// Use this method to specify where in the replay sequence the playback should start.
        /// This method accepts normalized offsets values between 0 and 1 and performs validation before using the value.
        /// </summary>
        /// <param name="normalizedOffset">The normalized value representing the offset from the specified origin to start the playback from</param>
        /// <param name="origin">The playback node to take the offset from. If <see cref="PlaybackOrigin.End"/> is specified then the offset value will be used as a negative offset</param>
        public static void SetPlaybackFrameNormalized(float normalizedOffset, PlaybackOrigin origin = PlaybackOrigin.Start)
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Move our current sequence frame to the normalized value
            ReplaySnapshot frame = Instance.sequence.SeekPlayback(normalizedOffset, origin, true);

            // Check for a valid frame
            if (frame != null)
            {
                // Restore the scene state
                Instance.scene.RestoreSnapshot(frame, Target.InitialStateBuffer);

                // Call the reset event
                ReplayBehaviour.Events.CallReplayResetEvents();

                // This calls 'updateReplay' on all replay obejcts in the scene
                ReplayBehaviour.Events.CallReplayUpdateEvents();
            }
        }

        /// <summary>
        /// Use this method to set the current playback at a specific replay frame.
        /// This will allow the state of a specific replay frame to be restored but will not continue playback which will provide a freeze frame effect.
        /// Use <see cref="SetPlaybackFrame(float, PlaybackOrigin)"/> or <see cref="SetPlaybackFrameNormalized(float, PlaybackOrigin)"/> before calling this method to specify the exact location at which the playback frame should be sampled.  
        /// Use <see cref="StopPlayback"/> to unfreeze the still frame and return to normal game mode. 
        /// This method will ignore the value of <see cref="playbackEndBehaviour"/> as only a single frame is replayed. As a result you will need to call <see cref="StopPlayback"/> when you want to end the playback frame.  
        /// </summary>
        public static void BeginPlaybackFrame()
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Make sure we have some recorded data
            if (Instance.target.MemorySize == 0)
            {
                Debug.LogWarning(string.Format("[{0}]: Playback cannot begin because there is no recorded data", typeof(ReplayManager)));
                return;
            }

            if (instance.state == PlaybackState.Recording_Paused)
            {
                instance.state = PlaybackState.Recording_Paused_Playback;
            }
            else
            {
                // Check if we are recording
                if (IsRecording == true)
                    StopRecording();
            }

            // Inform the replay target that we are about to begin playback
            Instance.target.PrepareTarget(ReplayTargetTask.PrepareRead);

            // Enable replay mode
            Instance.scene.SetReplaySceneMode(
                ReplayScene.ReplaySceneMode.Playback,
                Instance.target.InitialStateBuffer);


            // Activate single playback mode
            Instance.singlePlayback = true;

            // Update our current state
            if(instance.state != PlaybackState.Recording_Paused_Playback)
                Instance.state = PlaybackState.Playback;

#if UNITY_5_3_OR_NEWER == true
            string sceneName = SceneManager.GetActiveScene().name;
#else
            string sceneName = Application.loadedLevelName;
#endif

            // Check for correct scene
            if(Instance.target.TargetSceneName != sceneName)
            {
                // The replay was recorded in a different scene
                Debug.LogWarning(string.Format("The replay file was recorded from a different scene called '{0}'. Playback may contain errors", Instance.target.TargetSceneName));
            }
        }

        /// <summary>
        /// Use this method to begin the playback of the recorded objects.
        /// Use <see cref="SetPlaybackFrame(float, PlaybackOrigin)"/> or <see cref="SetPlaybackFrameNormalized(float, PlaybackOrigin)"/> before calling this method to specify the exact location at which playback should begin.
        /// This method will run the entire playback gathered and then automatically stop playback on completion if <see cref="playbackEndBehaviour"/> is true.
        /// </summary>
        /// <param name="fromStart"> When true, the replay will be played from the first frame recorded</param>
        public static void BeginPlayback(bool fromStart = true)
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            if (instance.state == PlaybackState.Recording_Paused)
            {
                // Check if we are recording
                if (IsRecording == true)
                {
                    // Stop recording before playback
                    StopRecording();
                }

                instance.state = PlaybackState.Recording_Paused_Playback;
            }
            else
            {
                // Check if we are recording
                if (IsRecording == true)
                {
                    // Stop recording before playback
                    StopRecording();
                }
            }

            // Check if we are replaying
            if (IsReplaying == false)
            {
                // Inform the replay target that we are about to begin playback
                Instance.target.PrepareTarget(ReplayTargetTask.PrepareRead);

                // Enable replay mode - prepares scene for playback - Be sure to enable replay mode before settings the active replay frame
                Instance.scene.SetReplaySceneMode(
                    ReplayScene.ReplaySceneMode.Playback,
                    Instance.target.InitialStateBuffer);

                // Call the reset event
                ReplayBehaviour.Events.CallReplayResetEvents();

                // Broadcast the replay start event
                ReplayBehaviour.Events.CallReplayStartEvents();
            }

            // Check if we should seek to the start
            if (fromStart == true)
                SetPlaybackFrame(0);

            // Disable single playback
            Instance.singlePlayback = false;

            // Update our current state
            if(instance.state != PlaybackState.Recording_Paused_Playback)
                Instance.state = PlaybackState.Playback;
        }

        /// <summary>
        /// Use this method to begin the playback of the recorded objects.
        /// Use <see cref="SetPlaybackFrame(float, PlaybackOrigin)"/> or <see cref="SetPlaybackFrameNormalized(float, PlaybackOrigin)"/> before calling this method to specify the exact location at which playback should begin.
        /// This method will run the entire playback gathered and then automatically stop playback on completion if <see cref="playbackEndBehaviour"/> is true.
        /// </summary>
        /// <param name="fromStart"> When true, the replay will be played from the first frame recorded</param>
        /// <param name="direction">The direction that the replay should be played</param>
        [Obsolete("Use 'BeginPlayback(bool)' instead. If you need to change the playback direction use 'Time.timeScale' where negative values will cause playback to rewind")]
        public static void BeginPlayback(bool fromStart, PlaybackDirection direction)
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            if (instance.state == PlaybackState.Recording_Paused)
            {
                instance.state = PlaybackState.Recording_Paused_Playback;
            }
            else
            {
                // Check if we are recording
                if (IsRecording == true)
                {
                    // Stop recording before playback
                    StopRecording();
                }
            }

            // Check if we are replaying
            if (IsReplaying == false)
            {
                // Inform the replay target that we are about to begin playback
                Instance.target.PrepareTarget(ReplayTargetTask.PrepareRead);

                // Enable replay mode - prepares scene for playback - Be sure to enable replay mode before settings the active replay frame
                Instance.scene.SetReplaySceneMode(
                    ReplayScene.ReplaySceneMode.Playback,
                    Instance.target.InitialStateBuffer);

                // Call the reset event
                ReplayBehaviour.Events.CallReplayResetEvents();

                // Broadcast the replay start event
                ReplayBehaviour.Events.CallReplayStartEvents();
            }

            // Check if we should seek to the start
            if (fromStart == true)
                SetPlaybackFrame(0);

            // Set the playback direction for the replay sequence
            if (direction == PlaybackDirection.Forward)
            {
                if (Time.timeScale < 0)
                    Time.timeScale = 1f;
            }
            else if(direction == PlaybackDirection.Backward)
            {
                if (Time.timeScale > 0)
                    Time.timeScale = -1f;
            }
            

            // Disable single playback
            Instance.singlePlayback = false;

            // Update our current state
            if(instance.state != PlaybackState.Recording_Paused_Playback)
                Instance.state = PlaybackState.Playback;
        }

        /// <summary>
        /// Use this method to pause replay playback while maintinaing the current replay state.
        /// See <see cref="ResumePlayback"/> to continue a playback.
        /// </summary>
        public static void PausePlayback()
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Make sure we are in playback mode
            if (Instance.state == PlaybackState.Playback)
            {
                // Update the state
                Instance.state = PlaybackState.Playback_Paused;

                // Broadcast the pause event
                ReplayBehaviour.Events.CallReplayPlayPauseEvents(true);
            }
        }

        /// <summary>
        /// Use this method to resume playback after a previous call to <see cref="PausePlayback"/> was called.
        /// If <see cref="PausePlayback"/> was not called prior to this method then the method will have no effect.
        /// </summary>
        public static void ResumePlayback()
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Make sure we are in playback paused mode
            if (Instance.state == PlaybackState.Playback_Paused)
            {
                // Update state
                Instance.state = PlaybackState.Playback;

                // Broadcast the resume event
                ReplayBehaviour.Events.CallReplayPlayPauseEvents(false);
            }
        }

        /// <summary>
        /// Use this method to stop any active playback.
        /// This method will only have an effect if there is an active playback running otherwise it will have no effect.
        /// </summary>
        public static void StopPlayback(bool restorePreviousSceneState = true)
        {
            // Make sure we have a manager
            if (Instance == null)
                return;

            // Make sure we are replaying
            if (IsReplaying == true || instance.state == PlaybackState.Recording_Paused_Playback)
            {
                // Call the end eventd
                ReplayBehaviour.Events.CallReplayEndEvents();

                // Disable replay mode - Triggers gameplay prepearation
                Instance.scene.restorePreviousSceneState = restorePreviousSceneState;

                // Switch the scene back to live game mode
                Instance.scene.SetReplaySceneMode(
                    ReplayScene.ReplaySceneMode.Live,
                    Instance.target.InitialStateBuffer);

                if (instance.state == PlaybackState.Recording_Paused_Playback)
                {
                    instance.state = PlaybackState.Recording_Paused;
                }
                else
                {
                    // Change to idle state
                    Instance.state = PlaybackState.Idle;
                }
            }
        }

        /// <summary>
        /// Attempts to forcefully create or get a reference to a valid replay manager instance.
        /// If no scene instance is found then a new object will be created.
        /// </summary>
        public static ReplayManager ForceAwake()
        {
            isSceneDisposing = false;
            return Instance;
        }

        /// <summary>
        /// Attempts to register a game object as a prefab so that the replay system is able to spawn or despawn the object as needed.
        /// You only need to do this for objects that are likley to be either instantiated or destroyed during recording. 
        /// The replay system will then be able to accuratley restore the scene state during playback.
        /// The specified object must be a prefab otherwise an error will be thrown and the object will not be registered.
        /// Prefab instances are not accepted.
        /// </summary>
        /// <param name="prefab">The prefab object to register with the replay system</param>
        public static void RegisterReplayPrefab(GameObject prefab)
        {
            // Make sure we have a replay manager
            if (prefab == null || Instance == null)
                return;

            // Get the replay prefab component
            ReplayObject component = prefab.GetComponent<ReplayObject>();

            // Check for component
            if(component == null)
            {
                Debug.LogWarning(string.Format("Prefab '{0}' cannot be registered for replay because it does not have a 'ReplayObject' component attached to it", prefab.name));
                return;
            }

            // Check for no prefab
            if(component.IsPrefab == false)
            {
                Debug.LogWarning(string.Format("Object '{0}' cannot be registered as a replay prefab because it is not a prefab object", prefab.name));
                return;
            }

            // Register the prefab
            if (Instance.replayPrefabs.Contains(component) == false)
                Instance.replayPrefabs.Add(component);
        }

        /// <summary>
        /// Attempts to find the prefab with the matching name.
        /// This is used to restore objects that were destroyed during recording.
        /// </summary>
        /// <param name="prefabName">The name of the prefab to locate</param>
        /// <returns>The matching prefab or null if no matching prefab was found</returns>
        public static GameObject FindReplayPrefab(string prefabName)
        {
            // Make sure we have a replay manager
            if (Instance == null)
                return null;

            // Process all objects
            foreach(ReplayObject prefab in Instance.replayPrefabs)
            {
                // Check for a matching prefab name
                if(string.Compare(prefab.PrefabIdentity, prefabName) == 0)
                {
                    // Get the prefab object
                    return prefab.gameObject;
                }
            }

            // No prefab found
            return null;
        }

        /// <summary>
        /// Attempts to instantiate the specified prefab.
        /// <see cref="OnReplayInstantiate"/> will be called if a listener has been registered otherwise default instantiation will be used.
        /// </summary>
        /// <param name="prefab">The prefab to instantiate</param>
        /// <param name="position">The position to spawn the prefab at</param>
        /// <param name="rotation">The rotation to spawn the prefab with</param>
        /// <returns>The new instance of the specified prefab or null if an error occurred</returns>
        public static GameObject ReplayInstantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            // Check for null prefab
            if (prefab == null)
                throw new ArgumentException("The thing you want to instantiate is null");

            GameObject result = null;
            bool defaultInstantiate = false;

            // Check for event listener
            if (OnReplayInstantiate != null)
            {
                try
                {
                    // Call the user method
                    result = OnReplayInstantiate(prefab, position, rotation);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(string.Format("An exception was thrown by the override instantiation handler ({0}). Default instantiation will be used", e));
                    defaultInstantiate = true;
                }
            }
            else
                defaultInstantiate = true;

            // Use default instantiation
            if(defaultInstantiate == true)
            {
                // Create the instance
                result = GameObject.Instantiate(prefab, position, rotation) as GameObject;
            }

            return result;
        }

        /// <summary>
        /// Attempts to destroy the specified prefab.
        /// <see cref="OnReplayDestroy"/> will be called if a listener has been registered otherwise default destruction will be used.
        /// </summary>
        /// <param name="go">The game object to destroy</param>
        public static void ReplayDestroy(GameObject go)
        {
            // Check for null object - no harm in doing nothing
            if (go == null)
                return;

            bool defaultDestroy = false;

            // Check for event listener
            if (OnReplayDestroy != null)
            {
                try
                {
                    // Call the user method
                    OnReplayDestroy(go);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(string.Format("An exception was thrown by the override destroy handler ({0}). Default destruction will be used", e));
                    defaultDestroy = true;
                }
            }
            else
                defaultDestroy = true;

            if(defaultDestroy == true)
            {
                // Destroy the game object
                GameObject.Destroy(go);
            }
        }

        private void OnActiveSceneChanged()
        {
            // Start a fresh recording in the new scene
            if(IsDisposing == false)
                DiscardRecording();
        }
    }
}
