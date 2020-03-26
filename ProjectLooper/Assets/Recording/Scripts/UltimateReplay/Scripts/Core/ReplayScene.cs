using System.Collections.Generic;
using UnityEngine;
using UltimateReplay.Storage;

namespace UltimateReplay.Core
{
    /// <summary>
    /// A <see cref="ReplayScene"/> contains information about all active replay objects. 
    /// </summary>
    public sealed class ReplayScene
    {
        public enum ReplaySceneMode
        {
            Live,
            Playback,
        }

        // Private
        private List<ReplayObject> replayObjects = new List<ReplayObject>();
        private Queue<ReplayObject> dynamicReplayObjects = new Queue<ReplayObject>();
        private ReplaySnapshot prePlaybackState = null;
        private bool isPlayback = false;

        // Public
        public bool restorePreviousSceneState = true;

        // Properties
        /// <summary>
        /// Enable or disable the replay scene in preparation for playback or live mode.
        /// When true, all replay objects will be prepared for playback causing certain components or scripts to be disabled to prevent interference from game systems.
        /// A prime candidate would be the RigidBody component which could cause a replay object to be affected by gravity and as a result deviate from its intended position.
        /// When false, all replay objects will be returned to their 'Live' state when all game systems will be reactivated.
        /// </summary>
        public bool ReplayEnabled
        {
            get { return isPlayback; }
        }

        /// <summary>
        /// Get a collection of all game objects that are registered with the replay system.
        /// </summary>
        public List<ReplayObject> ActiveReplayObjects
        {
            get { return replayObjects; }
        }

        // Methods
        /// <summary>
        /// Registers a replay object with the replay system so that it can be recorded for playback.
        /// Typically all <see cref="ReplayObject"/> will auto register when they 'Awake' meaning that you will not need to manually register objects. 
        /// </summary>
        /// <param name="replayObject">The <see cref="ReplayObject"/> to register</param>
        public void RegisterReplayObject(ReplayObject replayObject)
        {
            // Add the replay object
            replayObjects.Add(replayObject);

            // Check if we are adding objects during playback
            if(isPlayback == true)
            {
                // We need to prepare the object for playback
                ReplayManager.Preparer.PrepareForPlayback(replayObject);
            }
            // Check if we are adding objects during recording
            else// if(ReplayManager.IsRecording == true)
            {
                // The object was added during recording
                dynamicReplayObjects.Enqueue(replayObject);
            }
        }

        /// <summary>
        /// Unregisters a replay object from the replay system so that it will no longer be recorded for playback.
        /// Typically all <see cref="ReplayObject"/> will auto un-register when they are destroyed so you will normally not need to un-register a replay object. 
        /// </summary>
        /// <param name="replayObject"></param>
        public void UnregisterReplayObject(ReplayObject replayObject)
        {
            // Remove the replay object
            if(replayObjects.Contains(replayObject) == true)
                replayObjects.Remove(replayObject);
        }

        public void SetReplaySceneMode(ReplaySceneMode mode, ReplayInitialDataBuffer initialStateBuffer)
        {
            if (mode == ReplaySceneMode.Playback)
            {
                // Get the scene ready for playback
                PrepareForPlayback(initialStateBuffer);
                isPlayback = true;
            }
            else
            {
                // Get the scene ready for gameplay
                PrepareForGameplay(initialStateBuffer);
                isPlayback = false;
            }
        }

        private void PrepareForPlayback(ReplayInitialDataBuffer initialStateBuffer)
        {
            // Sample the current scene
            prePlaybackState = RecordSnapshot(0, initialStateBuffer);

            for (int i = 0; i < replayObjects.Count; i++)
            {
                // Prepare the object for playback
                ReplayManager.Preparer.PrepareForPlayback(replayObjects[i]);
            }
        }

        private void PrepareForGameplay(ReplayInitialDataBuffer initialStateBuffer)
        {
            // Check if we can restore the previous scene state
            if (prePlaybackState != null)
            {
                // Restore the original game state
                if (restorePreviousSceneState == true)
                    RestoreSnapshot(prePlaybackState, initialStateBuffer);

                // Reset to null so that next states are saved
                prePlaybackState = null;

                // Set delta so that changes are immediate
                ReplayTime.Delta = 1;

                // Be sure to update so interpolated objects can also update
                ReplayBehaviour.Events.CallReplayUpdateEvents();
            }

            for (int i = 0; i < replayObjects.Count; i++)
                ReplayManager.Preparer.PrepareForGameplay(replayObjects[i]);
        }

        /// <summary>
        /// Take a snapshot of the current replay scene using the specified timestamp.
        /// </summary>
        /// <param name="timeStamp">The timestamp for the frame indicating its position in the playback sequence</param>
        /// <param name="initialStateBuffer">The <see cref="ReplayInitialDataBuffer"/> to restore dynamic object information from</param>
        /// <returns>A new snapshot of the current replay scene</returns>
        public ReplaySnapshot RecordSnapshot(float timeStamp, ReplayInitialDataBuffer initialStateBuffer)
        {
            ReplaySnapshot snapshot = new ReplaySnapshot(timeStamp);

            if (initialStateBuffer != null)
            {
                // Be sure to record any objects initial transform if they were spawned during the snapshot
                while (dynamicReplayObjects.Count > 0)
                {
                    // Get the next object
                    ReplayObject obj = dynamicReplayObjects.Dequeue();

                    // Make sure the object has not been destroyed
                    if (obj != null)
                    {
                        // Record initial values
                        initialStateBuffer.RecordInitialReplayObjectData(obj, timeStamp, obj.transform.position, obj.transform.rotation, obj.transform.localScale);
                    }
                }
            }


            // Record each object in the scene
            foreach (ReplayObject obj in replayObjects)
            {
                ReplayState state = new ReplayState();

                // Serialize the object
                obj.OnReplaySerialize(state);

                // Check if the state contains any information - If not then dont waste valuable memory
                if (state.Size == 0)
                    continue;

                // Record the snapshot
                snapshot.RecordSnapshot(obj.ReplayIdentity, state);
            }

            return snapshot;
        }

        /// <summary>
        /// Restore the scene to the state described by the specified snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to restore</param>
        /// <param name="initialStateBuffer">The <see cref="ReplayInitialDataBuffer"/> to restore dynamic object information from</param>
        public void RestoreSnapshot(ReplaySnapshot snapshot, ReplayInitialDataBuffer initialStateBuffer)
        {
            // Restore all events first
            snapshot.RestoreReplayObjects(this, initialStateBuffer);

            // Restore all replay objects
            foreach (ReplayObject obj in replayObjects)
            {
                // Get the state based on the identity
                ReplayState state = snapshot.RestoreSnapshot(obj.ReplayIdentity);

                // Check if no state information for this object was found
                if (state == null)
                    continue;

                // Deserialize the object
                obj.OnReplayDeserialize(state);
            }
        }
    }
}
