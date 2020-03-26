using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UltimateReplay.Core;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateReplay
{
    [Flags]
    [Serializable]
    internal enum ReplayBehaviourDataFlags : byte
    {
        None = 0,
        Variables = 1 << 0,
        Events = 1 << 1,

        // Maximum = 1 << 8
    }

    /// <summary>
    /// This interface can be implemented by mono behaviour scripts in order to receive replay start and end events.
    /// It works in a similar way to the 'Start' or 'Update' method however you must explicitly implement the interface as opposed to using magic methods.
    /// This allows for slightly improved performance.
    /// </summary>
    [ExecuteInEditMode]
    public abstract class ReplayBehaviour : MonoBehaviour, IReplaySerialize
    {
        // Private
        private const byte variableIdentifier = 0x01;
        private const byte eventIdentifier = 0x02;

        private static HashSet<ReplayBehaviour> allBehaviours = new HashSet<ReplayBehaviour>();

        [SerializeField]
        private ReplayIdentity replayIdentity = new ReplayIdentity();
        private ReplayVariable[] replayVariables = new ReplayVariable[0];

        private Queue<ReplayEvent> replayEvents = new Queue<ReplayEvent>();

        // Properties 
        /// <summary>
        /// Get the <see cref="ReplayIdentity"/> associated with this <see cref="ReplayBehaviour"/>.  
        /// </summary>
        public ReplayIdentity Identity
        {
            get { return replayIdentity; }
            set { replayIdentity = value; }
        }

        /// <summary>
        /// Returns true if the active replay manager is currently recording the scene.
        /// Note: If recording is paused this value will still be true.
        /// </summary>
        public bool IsRecording
        {
            get
            {
                // Check for disposing manager
                if (ReplayManager.IsDisposing == true)
                    return false;

                // Get recording state
                return ReplayManager.IsRecording;
            }
        }

        /// <summary>
        /// Returns true if the active replay manager is currently replaying a previous recording.
        /// Note: If playback is paused this value will still be true.
        /// </summary>
        public bool IsReplaying
        {
            get
            {
                // Check for disposing manager
                if (ReplayManager.IsDisposing == true)
                    return false;

                // Get replaying state
                return ReplayManager.IsReplaying;
            }
        }

        /// <summary>
        /// Gets the current <see cref="PlaybackDirection"/> of replay playback.
        /// </summary>
        public PlaybackDirection PlaybackDirection
        {
            get
            {
                // Check for disposing manager
                if (ReplayManager.IsDisposing == true)
                    return PlaybackDirection.Forward;

                // Get direction
                return ReplayManager.PlaybackDirection;
            }
        }

        // Methods
        /// <summary>
        /// Called by Unity while in editor mode.
        /// Allows the unique id to be generated when the script is attached to an object.
        /// </summary>
        public virtual void Reset()
        {
            
            // Generate the unique id if required - This is requried for objects instantiated during replay
            //replayIdentity.Generate();

            // Try to find a parent replay object or create one if required
#if UNITY_EDITOR
            // Check for ignored components
            if (GetType().IsDefined(typeof(ReplayIgnoreAttribute), true) == true)
                return;

            ReplayObject manager = GetManagingObject();

            // Create a manager component
            if (manager == null)
                manager = Undo.AddComponent<ReplayObject>(gameObject);

            // Force rebuild the component list
            if (manager != null)
                manager.RebuildComponentList();
#endif
        }

        /// <summary>
        /// Called by Unity.
        /// Allows the <see cref="ReplayBehaviour"/> to validate its identity and register its self with the replay system.
        /// </summary>
        public virtual void Awake()
        {
            ReplayIdentity.RegisterIdentity(replayIdentity);
            // Generate the unique id if required - This is requried for objects instantiated during replay
            //replayIdentity.Generate();

            // Find all replay variables and cache them
            ReplayFindVariables();
        }

        public virtual void OnDestroy()
        {
            ReplayIdentity.UnregisterIdentity(replayIdentity);
        }

        /// <summary>
        /// Called by Unity.
        /// Be sure to call this base method when overriding otherwise replay events will not be received.
        /// </summary>
        public virtual void OnEnable()
        {
            // Register this behaviour
            if(allBehaviours.Contains(this) == false)
                allBehaviours.Add(this);
        }

        /// <summary>
        /// Called by Unity.
        /// Be sure to call this base method when overriding otherwise replay events will not be received.
        /// </summary>
        public virtual void OnDisable()
        {
            // Un-register this behaviour
            if(allBehaviours.Contains(this) == true)
                allBehaviours.Remove(this);
        }

        /// <summary>
        /// Called by the replay system when all replay state information should be serialized.
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to write the data to</param>
        public virtual void OnReplaySerialize(ReplayState state)
        {
#if UNITY_EDITOR
            // Serialize may be called by the editor in which case we will need to find variables on each call
            if (Application.isPlaying == false)
                ReplayFindVariables();
#endif

            ReplayBehaviourDataFlags flags = ReplayBehaviourDataFlags.None;

            // Generate the data flags so we can read the correct data back
            if (replayEvents.Count > 0) flags |= ReplayBehaviourDataFlags.Events;
            if (replayVariables.Length > 0) flags |= ReplayBehaviourDataFlags.Variables;

            // Always write the flag byte
            state.Write((byte)flags);

            // Serialize replay events
            if((flags & ReplayBehaviourDataFlags.Events) != 0)
            {
                // Serialize all events
                ReplaySerializeEvents(state);
            }

            // Serialize all replay variables by default
            if((flags & ReplayBehaviourDataFlags.Variables) != 0)
            {
                // Serialize all variables
                ReplaySerializeVariables(state);
            }
        }

        /// <summary>
        /// Called by the replay system when all replay state information should be deserialized.
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to read the data from</param>
        public virtual void OnReplayDeserialize(ReplayState state)
        {
            // Check if we can read
            if (state.EndRead == true)
                return;

            // Get the flag byte
            ReplayBehaviourDataFlags flags = (ReplayBehaviourDataFlags)state.ReadByte();
            
            // Deserialize events if there are any
            if ((flags & ReplayBehaviourDataFlags.Events) != 0)
                ReplayDeserializeEvents(state);

            // Deserialize variables if there are any
            if ((flags & ReplayBehaviourDataFlags.Variables) != 0)
                ReplayDeserializeVariables(state);
        }

        /// <summary>
        /// Called by the replay system when playback is about to start.
        /// You can disable game behaviour that should not run during playback in this method, such as player movement.
        /// </summary>
        public virtual void OnReplayStart() { }

        /// <summary>
        /// Called by the replay system when playback has ended.
        /// You can re-enable game behaviour in this method to allow the gameplay to 'take over'
        /// </summary>
        public virtual void OnReplayEnd() { }

        /// <summary>
        /// Called by the replay system when playback is about to be paused or resumed.
        /// </summary>
        /// <param name="paused">True if playback is about to be paused or false if plyabck is about to be resumed</param>
        public virtual void OnReplayPlayPause(bool paused) { }

        /// <summary>
        /// Called by the replay system during playback when cached values should be reset to safe default to avoid glitches or inaccuracies in the playback.
        /// </summary>
        public virtual void OnReplayReset() { }

        /// <summary>
        /// Called by the replay system every frame while playback is active.
        /// </summary>
        public virtual void OnReplayUpdate()
        {
            // Make sure all variables are interpolated correctly
            ReplayInterpolateVariables(ReplayTime.Delta);
        }

        /// <summary>
        /// Called by the replay system when an event has been received during playback.
        /// </summary>
        /// <param name="replayEvent">The event that was received</param>
        public virtual void OnReplayEvent(ReplayEvent replayEvent) { }

        /// <summary>
        /// Called by the replay system when the object has been spawned from a prefab instance during playback.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public virtual void OnReplaySpawned(Vector3 position, Quaternion rotation) { }

        /// <summary>
        /// Push an event to the replay system for recording.
        /// </summary>
        /// <param name="eventID">The event id to uniquley identify the event type</param>
        /// <param name="state">The state data for the event or null if no state data is required</param>
        public void ReplayRecordEvent(ReplayEvents eventID, ReplayState state = null)
        {
            // Call through
            ReplayRecordEvent((byte)eventID, state);
        }

        /// <summary>
        /// Push an event to the replay system for recording.
        /// </summary>
        /// <param name="eventID">The event id to uniquley identify the event type</param>
        /// <param name="state">The state data for the event or null if no state data is required</param>
        public void ReplayRecordEvent(byte eventID, ReplayState state = null)
        {
            // Create the replay event
            ReplayEvent evt = new ReplayEvent
            {
                eventID = eventID,
                eventData = state,
            };

            // Check for null state
            if (evt.eventData == null)
                evt.eventData = new ReplayState();

            // Push to event queue
            replayEvents.Enqueue(evt);
        }

        /// <summary>
        /// Serializes all awaiting <see cref="ReplayEvent"/> to the state.
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to write the varaible data to</param>
        protected virtual void ReplaySerializeEvents(ReplayState state)
        {
            // Get the number of events
            short size = (short)replayEvents.Count;

            // Write the size to the state
            state.Write(size);

            // Write all events
            while(replayEvents.Count > 0)
            {
                // Get the next event
                ReplayEvent evt = replayEvents.Dequeue();

                // Write the event id
                state.Write(evt.eventID);

                // Write the size and event data
                state.Write((byte)evt.eventData.Size);
                state.Write(evt.eventData);
            }
        }

        /// <summary>
        /// Deserializes all active <see cref="ReplayEvent"/> from the state and dispatches any events to the <see cref="OnReplayEvent(ReplayEvent)"/> handler. 
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to read the variable data from</param>
        protected virtual void ReplayDeserializeEvents(ReplayState state)
        {
            // Read the event size
            short size = state.Read16();

            // Read all events
            for(int i = 0; i < size; i++)
            {
                // Create the event
                ReplayEvent evt = new ReplayEvent();

                // Read the event id
                evt.eventID = state.ReadByte();

                // Read the data size
                byte dataSize = state.ReadByte();

                // Read the event data
                evt.eventData = state.ReadState(dataSize);

                try
                {
                    // Trigger the event callback
                    OnReplayEvent(evt);
                }
                catch(Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// Serializes all active <see cref="ReplayVariable"/> to the state. 
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to write the varaible data to</param>
        protected virtual void ReplaySerializeVariables(ReplayState state)
        {
            // Get size
            short size = (short)replayVariables.Length;

            // Write the size to the state
            state.Write(size);

            // Write all variables
            foreach(ReplayVariable variable in replayVariables)
            {
                // Write the hash code of the name so we can identify the data later
                state.Write(variable.Name.GetHashCode());

                // Write the variable to the state
                variable.OnReplaySerialize(state);
            }
        }

        /// <summary>
        /// Deserializes all active <see cref="ReplayVariable"/> from the state. 
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to read the variable data from</param>
        protected virtual void ReplayDeserializeVariables(ReplayState state)
        {
            // Get the size
            short size = state.Read16();

            // Read all variables
            for(int i = 0; i < size; i++)
            {
                bool matchedVariable = false;

                // Read the variable name hash
                int variableHash = state.Read32();

                // Check for matching variable
                foreach(ReplayVariable variable in replayVariables)
                {
                    // We have found a matching variable
                    if(variable.Name.GetHashCode() == variableHash)
                    {
                        // Deserialize the variable
                        variable.OnReplayDeserialize(state);
                        matchedVariable = true;

                        break;
                    }
                }

                // We failed to resolve the state - we cannot continue reading because the state contains invalid data
                if (matchedVariable == false)
                    break;
            }
        }

        /// <summary>
        /// Allows all active <see cref="ReplayVariable"/> to be interpolated betwen replay frames. 
        /// </summary>
        /// <param name="delta">The 't' value between frames used for interpolation</param>
        protected virtual void ReplayInterpolateVariables(float delta)
        {
            foreach (ReplayVariable variable in replayVariables)
            {
                // Check if the variable should be interpolated
                if (variable.IsInterpolated == true)
                {
                    // Interpolate the variable
                    variable.Interpolate(delta);
                }
            }
        }

        /// <summary>
        /// Attempts to find any variables marked with the <see cref="ReplayVarAttribute"/> so that they can be serialized later. 
        /// </summary>
        protected virtual void ReplayFindVariables()
        {
            // Clear old array
            replayVariables = new ReplayVariable[0];

            foreach (FieldInfo field in GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                // Check if we have the ReplayVarAttribute
                if (field.IsDefined(typeof(ReplayVarAttribute), true) == true)
                {
#if UNITY_WINRT && !UNITY_EDITOR
                    ReplayVarAttribute varAttribute = field.GetCustomAttribute<ReplayVarAttribute>();

                    // Check for unexpected error
                    if (varAttribute == null)
                        continue;
#else
                    // Get the attribute
                    object[] attributes = field.GetCustomAttributes(typeof(ReplayVarAttribute), true);

                    // Check for unexpected error
                    if (attributes.Length == 0)
                        continue;

                    // Get the first attribute (Only 1 allowed)
                    ReplayVarAttribute varAttribute = attributes[0] as ReplayVarAttribute;
#endif

                    // Create a variable for this field
                    ReplayVariable variable = new ReplayVariable(this, field, varAttribute);

                    // Add to array for serialization
                    ReplayRegisterVariable(variable);
                }
            }
        }

        private void ReplayRegisterVariable(ReplayVariable variable)
        {
            // Resize the array
            Array.Resize(ref replayVariables, replayVariables.Length + 1);

            // Add to back
            replayVariables[replayVariables.Length - 1] = variable;
        }

#if UNITY_EDITOR
        private ReplayObject GetManagingObject()
        {
            ReplayObject manager = null;

            Transform current = transform;

            do
            {
                // Check for a replay object to manage this component
                if (current.GetComponent<ReplayObject>() != null)
                {
                    // Get the manager object
                    manager = current.GetComponent<ReplayObject>();
                    break;
                }

                // Look higher in the hierarchy
                current = current.parent;
            }
            while (current != null);

            // Get the manager
            return manager;
        }
#endif

        /// <summary>
        /// Used for organisation only.
        /// Hides the event method from the <see cref="ReplayBehaviour"/> class. 
        /// </summary>
        internal static class Events
        {
            /// <summary>
            /// Use this method to inform all active <see cref="ReplayBehaviour"/> scripts that playback is about to start. 
            /// </summary>
            internal static void CallReplayStartEvents()
            {
                // Find all replay behaviours
                foreach (ReplayBehaviour behaviour in allBehaviours)
                {
                    try
                    {
                        // Call the start method - catch any exceptions so that all behaviours will always receive the event
                        behaviour.OnReplayStart();
                    }
                    catch (Exception e)
                    {
                        // Log the exception to the console
                        Debug.LogException(e);
                    }
                }
            }

            /// <summary>
            /// Use this method to inform all active <see cref="ReplayBehaviour"/> scripts that playback is about to end. 
            /// </summary>
            internal static void CallReplayEndEvents()
            {
                // Find all replay behaviours
                foreach (ReplayBehaviour behaviour in allBehaviours)
                {
                    try
                    {
                        // Call the end method - catch any exceptions so that all behaviours will always receive the event
                        behaviour.OnReplayEnd();
                    }
                    catch (Exception e)
                    {
                        // Log the exception to the console
                        Debug.LogException(e);
                    }
                }
            }

            /// <summary>
            /// Use this method to inform all active <see cref="ReplayBehaviour"/> scripts that playback is about to be paused or resumed. 
            /// </summary>
            /// <param name="paused">True if playback is about to pause of false if playback is about to resume</param>
            internal static void CallReplayPlayPauseEvents(bool paused)
            {
                // Find all replay behaviours
                foreach (ReplayBehaviour behaviour in allBehaviours)
                {
                    try
                    {
                        // Call the play pause method = catch any exceptions so that all behaviours will always receive the event
                        behaviour.OnReplayPlayPause(paused);
                    }
                    catch (Exception e)
                    {
                        // Log the exception to the console
                        Debug.LogException(e);
                    }
                }
            }

            /// <summary>
            /// Use this method to inform all active <see cref="ReplayBehaviour"/> scripts that a playback reset event has occured. 
            /// </summary>
            internal static void CallReplayResetEvents()
            {
                // Find all replay behaviours
                foreach (ReplayBehaviour behaviour in allBehaviours)
                {
                    try
                    {
                        // Call the play pause method = catch any exceptions so that all behaviours will always receive the event
                        behaviour.OnReplayReset();
                    }
                    catch (Exception e)
                    {
                        // Log the exception to the console
                        Debug.LogException(e);
                    }
                }
            }

            /// <summary>
            /// Use this method to update all <see cref="ReplayBehaviour"/> scripts.
            /// This method should be called every frame while playback is active.
            /// </summary>
            internal static void CallReplayUpdateEvents()
            {
                // Find all replay behaviours
                foreach (ReplayBehaviour behaviour in allBehaviours)
                {
                    try
                    {
                        // Call the update method - catch any exceptions so that all behaviours will always receive the event
                        behaviour.OnReplayUpdate();
                    }
                    catch (Exception e)
                    {
                        // Log the exception to the console
                        Debug.LogException(e);
                    }
                }
            }

            internal static void CallReplaySpawnedEvents(ReplayObject spawnedObject, Vector3 position, Quaternion rotation)
            {
                // Find all replay behaviours on the target object
                foreach (ReplayBehaviour behaviour in spawnedObject.GetComponentsInChildren<ReplayBehaviour>())
                {
                    try
                    {
                        // Call the spawned method - catch any exceptions so that all behaviours will always receive the event
                        behaviour.OnReplaySpawned(position, rotation);
                    }
                    catch (Exception e)
                    {
                        // Log the exception to the console
                        Debug.LogException(e);
                    }
                }
            }
        }
    }
}
