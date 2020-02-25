using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif

namespace UltimateReplay.Storage
{
    /// <summary>
    /// Represents a memory storage buffer where replay data can be stored for game sessions.
    /// The buffer can be used as a continuous rolling buffer of a fixed size where a fixed amount of playback footage is recorded and then overwritten by new data as it is received.
    /// </summary>
    [Serializable]
    public class ReplayMemoryTarget : ReplayTarget
    {
        // Private
        private List<ReplaySnapshot> states = new List<ReplaySnapshot>();
        private ReplayInitialDataBuffer initialStateBuffer = new ReplayInitialDataBuffer();
        private string sceneName = string.Empty;

        // Public
        /// <summary>
        /// The amount of time in seconds of recording that should be kept in memory before it is discarded.
        /// The time is measured backwards from the current time to give a rolling buffer of the last 'n' seconds.
        /// This is useful in situations where you are only need the previous few secnonds of gameplay to be recorded for example: A kill cam.
        /// If this value is set to 0 then the internal buffer will not be wrapped at all. You should take extra care when using an unconstrained buffer as there is potential to run into an <see cref="OutOfMemoryException"/>, especially on mobile platforms where memory is at a premium. 
        /// </summary>
        [Range(0f, 60)]
        [Tooltip("0 = RecordAll] How many seconds should be recorded in the replay. Higher values will result in higher memory consumption")]
        public float recordSeconds = 15;

        // Properties
        /// <summary>
        /// The amount of time in seconds that the recording lasts.
        /// Usually this value will be equal to <see cref="recordSeconds"/> however it will take atleast the amount of <see cref="recordSeconds"/> to initially fill the buffer before it wraps around.  
        /// </summary>
        public override float Duration
        {
            get
            {
                // Check for any information
                if (states.Count == 0)
                    return 0;

                // Get end frame
                ReplaySnapshot end = states[states.Count - 1];

                // Use the end frame as the default duration
                float duration = end.TimeStamp;

                // Check if the buffer should be constrained
                if (recordSeconds != 0 && end.TimeStamp > recordSeconds)
                {
                    // Get start frame
                    ReplaySnapshot start = states[0];

                    // Take the start time into account (0 based start offset)
                    duration -= start.TimeStamp;
                }

                // Get the recording duration
                return duration;
            }
        }

        /// <summary>
        /// Get the amount of size in bytes that this memory target requires for all state data.
        /// This size does not include internal structures used to store the data but exclusivley contains game state sizes.
        /// </summary>
        public override int MemorySize
        {
            get
            {
                int size = 0;

                // Calculate the total size used by all frames
                foreach (ReplaySnapshot state in states)
                    size += state.Size;

                return size;
            }
        }

        public override ReplayInitialDataBuffer InitialStateBuffer
        {
            get { return initialStateBuffer; }
        }

        /// <summary>
        /// Get the name of the scene that the recording was taken from.
        /// </summary>
        public override string TargetSceneName
        {
            get { return sceneName; }
        }

        // Methods
        /// <summary>
        /// Store a replay snapshot in the replay target.
        /// If the new snapshot causes the internal buffer to 'overflow' then the recoding clip will be wrapped so that the recording duration is no more than <see cref="recordSeconds"/>. 
        /// </summary>
        /// <param name="state">The snapshot to store</param>
        public override void RecordSnapshot(ReplaySnapshot state)
        {
            // Register the state
            states.Add(state);

            // Create a fixed size wrap around buffer if possible
            ConstrainBuffer();
        }

        /// <summary>
        /// Recall a snapshot from the replay target based on the specified replay offset.
        /// </summary>
        /// <param name="offset">The offset pointing to the individual snapshot to recall</param>
        /// <returns>The replay snapshot at the specified offset</returns>
        public override ReplaySnapshot RestoreSnapshot(float offset)
        {
            // Check for no replay data
            if (states.Count == 0)
                return null;

            // Check for past clip end
            if (offset > Duration)
                return null;
            

            // Default to first frame
            ReplaySnapshot current = states[0];

            // Check all states to find the best matching snapshot that has a time stamp greater than the specified offset (Always snap to newer frame)
            foreach(ReplaySnapshot snapshot in states)
            {
                // Store the current snapshot
                current = snapshot;

                // Check if the timestamp is passed the offset
                if (snapshot.TimeStamp >= offset)
                    break;                               
            }

            return current;
        }

        /// <summary>
        /// Clears all state information for the current recording essentially restoring the memory target to its initial state.
        /// </summary>
        public override void PrepareTarget(ReplayTargetTask mode)
        {
            // Only listen for discard events - We can switch between read and write instantly
            if (mode == ReplayTargetTask.Discard)
            {
                // Clear all recorded data
                states.Clear();
                duration = 0;
            }
            else if (mode == ReplayTargetTask.Commit)
            {
                // Modify the snapshot time stamps so that the recording is 0 offset based
                if(states.Count > 0)
                {
                    // Get the timestamp of the first frame
                    float offsetTime = states[0].TimeStamp;

                    // Deduct the time stamp from all other frames to make them offset based on 0 time
                    foreach (ReplaySnapshot snapshot in states)
                        snapshot.CorrectTimestamp(-offsetTime);
                }
            }
            else if(mode == ReplayTargetTask.PrepareWrite)
            {
#if UNITY_5_3_OR_NEWER
                // Get the current scene name
                sceneName = SceneManager.GetActiveScene().name;
#else
                // Get the current scene name
                sceneName = Application.loadedLevelName;
#endif
            }
        }

        private void ConstrainBuffer()
        {
            // Check for unlimited buffer
            if (recordSeconds == 0)
                return;

            // Make sure we have more than one state
            if (states.Count == 0)
                return;

            // Keep an additional .2 of a second to ensure we have atleast the requested amount of recording
            float timeCompensation = 0.2f;

            // Calculate the start time
            float targetStartTime = states[states.Count - 1].TimeStamp - (recordSeconds + timeCompensation);

            // Process all buffered frames
            for (int i = 0; i < states.Count; i++)
            {
                // Check for any expired frames
                if (states[i].TimeStamp <= targetStartTime)
                {
                    // The frame is too old so we can throw it away
                    states.RemoveAt(i);
                }
            }
        }
    }
}
