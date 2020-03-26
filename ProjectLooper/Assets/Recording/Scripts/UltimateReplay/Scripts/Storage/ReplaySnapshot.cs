using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UltimateReplay.Core;

namespace UltimateReplay.Storage
{
    /// <summary>
    /// Represents initial data that may be stored by an object.
    /// </summary>
    [Flags]
    public enum ReplayInitialDataFlags : byte
    {
        /// <summary>
        /// No initial data is stored.
        /// </summary>
        None = 0,
        /// <summary>
        /// Initial position is recorded.
        /// </summary>
        Position = 1,
        /// <summary>
        /// Initial rotation is recorded.
        /// </summary>
        Rotation = 2,
        /// <summary>
        /// Initial scale is recorded.
        /// </summary>
        Scale = 4,
        /// <summary>
        /// Initial parent is recorded.
        /// </summary>
        Parent = 8,
    }

    /// <summary>
    /// Represents the intial settings of a newly spawned replay object.
    /// When a game object is instantiated it must be given an initial position and rotation.
    /// </summary>
    public struct ReplayInitialData : IReplaySerialize
    {
        // Private
        private ReplayInitialDataFlags flags;

        // Public
        /// <summary>
        /// Initial replay object identity.
        /// </summary>
        public ReplayIdentity objectIdentity;
        /// <summary>
        /// The timestamp when this object was instantiated.
        /// </summary>
        public float timestamp;
        /// <summary>
        /// Initial position data.
        /// </summary>
        public Vector3 position;
        /// <summary>
        /// Initial rotation data.
        /// </summary>
        public Quaternion rotation;
        /// <summary>
        /// Initial scale data.
        /// </summary>
        public Vector3 scale;
        /// <summary>
        /// Initial parent data.
        /// </summary>
        public ReplayIdentity parentIdentity;
        /// <summary>
        /// The replay ids for all observed components ordered by array index.
        /// </summary>
        public ReplayIdentity[] observedComponentIdentities;

        // Properties
        public ReplayInitialDataFlags InitialFlags
        {
            get { return flags; }
        }

        // Methods
        public void UpdateDataFlags()
        {
            // Reset flags
            flags = ReplayInitialDataFlags.None;

            // Create the initial object flag data
            if (position != Vector3.zero) flags |= ReplayInitialDataFlags.Position;
            if (rotation != Quaternion.identity) flags |= ReplayInitialDataFlags.Rotation;
            if (scale != Vector3.one) flags |= ReplayInitialDataFlags.Scale;
            if (parentIdentity != null) flags |= ReplayInitialDataFlags.Parent;
        }

        public void OnReplaySerialize(ReplayState state)
        {
            // Write the object identity
            state.Write(objectIdentity);
            state.Write(timestamp);

            // Calcualte the flags
            flags = ReplayInitialDataFlags.None;

            // Mkae sure initial state flags are updated
            UpdateDataFlags();

            // Write the data flags
            state.Write((short)flags);

            // Write Position
            if ((flags & ReplayInitialDataFlags.Position) != 0)
                state.Write(position);

            // Write rotation
            if ((flags & ReplayInitialDataFlags.Rotation) != 0)
                state.Write(rotation);

            // Write scale
            if ((flags & ReplayInitialDataFlags.Scale) != 0)
                state.Write(scale);

            // Write parent
            if ((flags & ReplayInitialDataFlags.Parent) != 0)
                state.Write(parentIdentity);

            // Write the component identities
            int size = (observedComponentIdentities == null) ? 0 : observedComponentIdentities.Length;

            // Write the number of ids
            state.Write((short)size);

            // Write all ids
            for(int i = 0; i < size; i++)
            {
                // Write the identity
                state.Write(observedComponentIdentities[i]);
            }
        }

        public void OnReplayDeserialize(ReplayState state)
        {
            // Read the object identity
            objectIdentity = state.ReadIdentity();
            timestamp = state.ReadFloat();

            // Read the flags
            flags = (ReplayInitialDataFlags)state.Read16();

            // Read position
            if ((flags & ReplayInitialDataFlags.Position) != 0)
                position = state.ReadVec3();

            // Read rotation
            if ((flags & ReplayInitialDataFlags.Rotation) != 0)
                rotation = state.ReadQuat();

            // Read scale
            if ((flags & ReplayInitialDataFlags.Scale) != 0)
                scale = state.ReadVec3();

            // Read parent identity
            if ((flags & ReplayInitialDataFlags.Parent) != 0)
                parentIdentity = state.ReadIdentity();

            // Read the number of observed components
            int size = state.Read16();

            // Allocate the array
            observedComponentIdentities = new ReplayIdentity[size];

            // Read all ids
            for(int i = 0; i < size; i++)
            {
                // Read the identity
                observedComponentIdentities[i] = state.ReadIdentity();
            }
        }
    }

    internal struct ReplayCreatedObject
    {
        // Public
        public ReplayObject replayObject;
        public ReplayInitialData replayInitialData;
    }

    /// <summary>
    /// A frame state is a snapshot of a replay frame that is indexed based on its time stamp.
    /// By sequencing multiple frame states you can create the replay effect.
    /// </summary>
    [Serializable]
    public sealed class ReplaySnapshot : IReplaySerialize, IReplayDataSerialize
    {
        // Private
        private static Queue<ReplayObject> sharedDestroyQueue = new Queue<ReplayObject>();

        private float timeStamp = 0;
        private HashSet<ReplayCreatedObject> newReplayObjectsThisFrame = new HashSet<ReplayCreatedObject>();
        private Dictionary<ReplayIdentity, ReplayState> states = new Dictionary<ReplayIdentity, ReplayState>();
        
        /// <summary>
        /// The time stamp for this snapshot.
        /// The time stamp is used to identify the snapshot location in the sequence.
        /// </summary>
        public float TimeStamp
        {
            get { return timeStamp; }
        }

        /// <summary>
        /// Get the size in bytes of the snapshot data.
        /// </summary>
        public int Size
        {
            get
            {
                int size = 0;

                // Calcualte the size of each object
                foreach (ReplayState state in states.Values)
                    size += state.Size;

                return size;
            }
        }

        // Constructor
        /// <summary>
        /// Create a new snapshot with the specified time stamp.
        /// </summary>
        /// <param name="timeStamp">The time stamp to give to this snapshot</param>
        public ReplaySnapshot(float timeStamp)
        {
            this.timeStamp = timeStamp;
        }        

        // Methods
        /// <summary>
        /// Called by the replay system when this <see cref="ReplaySnapshot"/> should be serialized. 
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to write the data to</param>
        public void OnReplaySerialize(ReplayState state)
        {
            state.Write(timeStamp);

            // Write states size
            state.Write(states.Count);

            // Write all states
            foreach(KeyValuePair<ReplayIdentity, ReplayState> pair in states)
            {
                state.Write(pair.Key);

                // Write the size and bulk data
                state.Write((short)pair.Value.Size);
                state.Write(pair.Value);
            }
        }

        /// <summary>
        /// Called by the replay system when this <see cref="ReplaySnapshot"/> should be serialized to binary. 
        /// </summary>
        /// <param name="writer">The binary stream to write te data to</param>
        public void OnReplayDataSerialize(BinaryWriter writer)
        {
            writer.Write(timeStamp);

            // Write the states size
            writer.Write(states.Count);

            // Write all states
            foreach (KeyValuePair<ReplayIdentity, ReplayState> pair in states)
            {
                writer.Write(pair.Key);

                // Write the size and bulk data
                writer.Write((short)pair.Value.Size);
                writer.Write(pair.Value.ToArray());
            }
        }

        /// <summary>
        /// Called by the replay system when this <see cref="ReplaySnapshot"/> should be deserialized. 
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to read the data from</param>
        public void OnReplayDeserialize(ReplayState state)
        {
            timeStamp = state.ReadFloat();

            // Read states size
            int size = state.Read32();

            // Read all states
            for(int i = 0; i < size; i++)
            {
                // Read the identity
                ReplayIdentity identity = state.ReadIdentity();

                // Read size
                short stateSize = state.Read16();

                // Read the state data
                ReplayState stateData = state.ReadState(stateSize);

                // Add to states
                states.Add(identity, stateData);
            }
        }

        /// <summary>
        /// Called by the replay system when this <see cref="ReplaySnapshot"/> should be deserialized from binary. 
        /// </summary>
        /// <param name="reader">The binary stream to read the data from</param>
        public void OnReplayDataDeserialize(BinaryReader reader)
        {
            timeStamp = reader.ReadSingle();

            // Read states size
            int size = reader.ReadInt32();

            // Read all states
            for (int i = 0; i < size; i++)
            {
                // Read the identity
                ReplayIdentity identity = new ReplayIdentity(reader.ReadInt16());

                // Read the size
                short stateSize = reader.ReadInt16();

                // Read the state data
                ReplayState stateData = new ReplayState(reader.ReadBytes(stateSize));

                // Add to states
                states.Add(identity, stateData);
            }
        }

        /// <summary>
        /// Registers the specified replay state with this snapshot.
        /// The specified identity is used during playback to ensure that the replay objects receives the correct state to deserialize.
        /// </summary>
        /// <param name="identity">The identity of the object that was serialized</param>
        /// <param name="state">The state data for the object</param>
        public void RecordSnapshot(ReplayIdentity identity, ReplayState state)
        {
            // Register the state
            if (states.ContainsKey(identity) == false)
                states.Add(identity, state);
        }

        /// <summary>
        /// Attempts to recall the state information for the specified replay object identity.
        /// If the identity does not exist in the scene then the return value will be null.
        /// </summary>
        /// <param name="identity">The identity of the object to deserialize</param>
        /// <returns>The state information for the specified identity or null if the identity does not exist</returns>
        public ReplayState RestoreSnapshot(ReplayIdentity identity)
        {
            // Try to get the state
            if (states.ContainsKey(identity) == true)
            {
                // Get the state
                ReplayState state = states[identity];

                // Reset the object state for reading
                state.PrepareForRead();

                return state;
            }

            // No state found
            return null;
        }

        /// <summary>
        /// Attempts to restore any replay objects that were spawned or despawned during this snapshot.
        /// </summary>
        public void RestoreReplayObjects(ReplayScene scene, ReplayInitialDataBuffer initialStateBuffer)
        {
            // Get all active scene objects
            List<ReplayObject> activeReplayObjects = scene.ActiveReplayObjects;

            // Find all active replay objects
            foreach (ReplayObject obj in activeReplayObjects)
            {
                // Check if the object is no longer in the scene
                if (states.ContainsKey(obj.ReplayIdentity) == false)
                {
                    // Check for a prefab
                    if (obj.IsPrefab == false)
                        continue;

                    // We need to destroy the replay object
                    sharedDestroyQueue.Enqueue(obj);
                }
            }

            // Destroy all waiting objects
            while (sharedDestroyQueue.Count > 0)
                ReplayManager.ReplayDestroy(sharedDestroyQueue.Dequeue().gameObject);

            // Process all snapshot state data to check if we need to add any scene objects
            foreach(KeyValuePair<ReplayIdentity, ReplayState> replayObject in states)
            {
                bool found = false;

                // Check if the desiered object is active in the scene
                foreach(ReplayObject obj in activeReplayObjects)
                {
                    // Check for matching identity
                    if(obj.ReplayIdentity == replayObject.Key)
                    {
                        // The object is in the scene - do nothing
                        found = true;
                        break;
                    }
                }

                // We need to spawn the object
                if(found == false)
                {
                    // Get the replay state for the object because it contains the prefab information we need
                    ReplayState state = replayObject.Value;

                    // Reset the state for reading
                    state.PrepareForRead();

                    // Read the name of the prefab that we need to spawn
                    string name = state.ReadString();
                    
                    // Try to find the matching prefab in our replay manager
                    GameObject prefab = ReplayManager.FindReplayPrefab(name);

                    // Check if the prefab was found
                    if(prefab == null)
                    {
                        // Check for no prefab name
                        if (string.IsNullOrEmpty(name) == true)
                        {
                            // Name information is unknown
                            name = "Unkwnown Replay Object (" + replayObject.Key + ")";

                            // Display scene object warning
                            Debug.LogWarning(string.Format("Failed to recreate replay scene object '{0}'. The object could not be found within the current scene and it is not a registered replay prefab. You may need to reload the scene before playback to ensure that all recorded objects are present.", name));
                        }
                        else
                        {
                            // Display prefab object warning
                            Debug.LogWarning(string.Format("Failed to recreate replay prefab '{0}'. No such prefab is registered.", name));
                        }
                        continue;
                    }

                    // Restore initial data
                    ReplayInitialData initialData = new ReplayInitialData();

                    // Check for valid state buffer
                    if (initialStateBuffer != null && initialStateBuffer.HasInitialReplayObjectData(replayObject.Key) == true)
                    {
                        // Restore the objects state data
                        initialData = initialStateBuffer.RestoreInitialReplayObjectData(replayObject.Key, timeStamp);//  RestoreInitialReplayObjectData(replayObject.Key);
                    }

                    Vector3 position = Vector3.zero;
                    Quaternion rotation = Quaternion.identity;
                    Vector3 scale = Vector3.one;

                    // Update transform values
                    if ((initialData.InitialFlags & ReplayInitialDataFlags.Position) != 0) position = initialData.position;
                    if ((initialData.InitialFlags & ReplayInitialDataFlags.Rotation) != 0) rotation = initialData.rotation;
                    if ((initialData.InitialFlags & ReplayInitialDataFlags.Scale) != 0) scale = initialData.scale;

                    // Call the instantiate method
                    GameObject result = ReplayManager.ReplayInstantiate(prefab, position, rotation);

                    if(result == null)
                    {
                        Debug.LogWarning(string.Format("Replay instanitate failed for prefab '{0}'. Some replay objects may be missing", name));
                        continue;
                    }

                    // Be sure to apply initial scale also
                    result.transform.localScale = scale;
                    
                    // try to find the replay object script
                    ReplayObject obj = result.GetComponent<ReplayObject>();

                    // Check if we have the component
                    if (obj != null)
                    {
                        // Give the replay object its serialized identity so that we can send replay data to it
                        obj.ReplayIdentity = replayObject.Key;

                        // Map observed component identities
                        if(initialData.observedComponentIdentities != null)
                        {
                            int index = 0;

                            foreach(ReplayBehaviour behaviour in obj.ObservedComponents)
                            {
                                if(initialData.observedComponentIdentities.Length > index)
                                {
                                    behaviour.Identity = initialData.observedComponentIdentities[index];
                                }
                                index++;
                            }
                        }

                        // Register the created object
                        newReplayObjectsThisFrame.Add(new ReplayCreatedObject
                        {
                            replayObject = obj,
                            replayInitialData = initialData,
                        });

                        // Trigger spawned event
                        ReplayBehaviour.Events.CallReplaySpawnedEvents(obj, position, rotation);
                    }
                }
            }


            // Re-parent replay objects
            foreach(ReplayCreatedObject created in newReplayObjectsThisFrame)
            {
                // Check for a parent identity
                if(created.replayInitialData.parentIdentity != null)
                {
                    bool foundTargetParent = false;

                    // We need to find the references parent
                    foreach(ReplayObject obj in scene.ActiveReplayObjects)
                    {
                        if(obj.ReplayIdentity == created.replayInitialData.parentIdentity)
                        {
                            // Parent the objects
                            created.replayObject.transform.SetParent(obj.transform, false);

                            // Set the flag
                            foundTargetParent = true;
                            break;
                        }
                    }

                    // Check if we failed to find the parent object
                    if (foundTargetParent == false)
                    {
                        // The target parent object is missing
                        Debug.LogWarning(string.Format("Newly created replay object '{0}' references identity '{1}' as a transform parent but the object could not be found in the current scene. Has the target parent been deleted this frame?", created.replayObject.name, created.replayInitialData.parentIdentity));
                    }
                }
            }

            // Clear ll tracked replay objects this frame
            newReplayObjectsThisFrame.Clear();
        }

        /// <summary>
        /// Clears all state information from the snapshot but keeps the time stamp.
        /// </summary>
        public void Reset()
        {
            states.Clear();
        }

        /// <summary>
        /// Attempts to modify the current snapshot time stamp by offsetting by the specified value.
        /// Negative values will reduce the timestamp.
        /// </summary>
        /// <param name="offset">The value to modify the timestamp with</param>
        internal void CorrectTimestamp(float offset)
        {
            // Modify the timestamp
            timeStamp += offset;
        }
    }
}