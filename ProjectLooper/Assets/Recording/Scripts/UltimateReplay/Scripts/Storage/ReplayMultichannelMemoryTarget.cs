using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif

namespace UltimateReplay.Storage
{
    internal struct ReplayMemoryChannel
    {
        public List<ReplaySnapshot> states;
        public ReplayInitialDataBuffer initialStateBuffer;
        public string sceneName;        
    }

    /// <summary>
    /// Represents a memory storage buffer where multple replays can be stored for game sessions in different channels.
    /// Each channel the target holds is capable or storing a single replay so multiple replays can be stored in the same target by making use of multiple channels.
    /// The buffer can be used as a continuous rolling buffer of a fixed size where a fixed amount of playback footage is recorded and then overwritten by new data as it is received.
    /// </summary>
    [Serializable]
    public class ReplayMultichannelMemoryTarget : ReplayTarget
    {
        // Private
        private List<ReplayMemoryChannel> channels = new List<ReplayMemoryChannel>();
        private int activeChannel = 0;

        // Public
        public const int minNumberOfChannels = 1;

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
        /// Get the duration for the <see cref="ActiveChannel"/>. 
        /// The amount of time in seconds that the recording lasts.
        /// Usually this value will be equal to <see cref="recordSeconds"/> however it will take atleast the amount of <see cref="recordSeconds"/> to initially fill the buffer before it wraps around.  
        /// </summary>
        public override float Duration
        {
            get
            {
                // Check for any information
                if (CurrentChannel.states.Count == 0)
                    return 0;

                // Get end frame
                ReplaySnapshot end = CurrentChannel.states[CurrentChannel.states.Count - 1];

                // Use the end frame as the default duration
                float duration = end.TimeStamp;

                // Check if the buffer should be constrained
                if (recordSeconds != 0 && end.TimeStamp > recordSeconds)
                {
                    // Get start frame
                    ReplaySnapshot start = CurrentChannel.states[0];

                    // Take the start time into account (0 based start offset)
                    duration -= start.TimeStamp;
                }

                // Get the recording duration
                return duration;
            }
        }

        /// <summary>
        /// Get the memory size for the <see cref="ActiveChannel"/>. 
        /// Get the amount of size in bytes that this memory target requires for all state data.
        /// This size does not include internal structures used to store the data but exclusivley contains game state sizes.
        /// </summary>
        public override int MemorySize
        {
            get
            {
                int size = 0;

                // Calculate the total size used by all frames
                foreach (ReplaySnapshot state in CurrentChannel.states)
                    size += state.Size;

                return size;
            }
        }

        public override ReplayInitialDataBuffer InitialStateBuffer
        {
            get { return CurrentChannel.initialStateBuffer; }
        }

        /// <summary>
        /// Get the target scene name for the <see cref="ActiveChannel"/>. 
        /// Get the name of the scene that the recording was taken from.
        /// </summary>
        public override string TargetSceneName
        {
            get { return CurrentChannel.sceneName; }
        }

        /// <summary>
        /// Get the number of channels in this target.
        /// This value will always be greater that or equal to <see cref="minNumberOfChannels"/>. 
        /// </summary>
        public int ChannelCount
        {
            get { return channels.Count; }
        }

        /// <summary>
        /// Get the channel index for the current active channel.
        /// To change the active channel use <see cref="SetActiveChannel(int)"/>. 
        /// </summary>
        public int ActiveChannel
        {
            get { return activeChannel; }
        }

        private ReplayMemoryChannel CurrentChannel
        {
            get { return channels[activeChannel]; }
            set { channels[activeChannel] = value; }
        }

        // Methods
        /// <summary>
        /// Called by Unity.
        /// </summary>
        public override void Awake()
        {
            // Create default channel
            SetNumberOfChannels(minNumberOfChannels);

            // Call base method
            base.Awake();
        }

        /// <summary>
        /// Record a snapshot in the <see cref="ActiveChannel"/>. 
        /// Store a replay snapshot in the replay target.
        /// If the new snapshot causes the internal buffer to 'overflow' then the recoding clip will be wrapped so that the recording duration is no more than <see cref="recordSeconds"/>. 
        /// </summary>
        /// <param name="state">The snapshot to store</param>
        public override void RecordSnapshot(ReplaySnapshot state)
        {
            // Register the state
            CurrentChannel.states.Add(state);

            // Create a fixed size wrap around buffer if possible
            ConstrainBuffer();
        }

        /// <summary>
        /// Restore a snapshot from the <see cref="ActiveChannel"/>. 
        /// Recall a snapshot from the replay target based on the specified replay offset.
        /// </summary>
        /// <param name="offset">The offset pointing to the individual snapshot to recall</param>
        /// <returns>The replay snapshot at the specified offset</returns>
        public override ReplaySnapshot RestoreSnapshot(float offset)
        {
            // Check for no replay data
            if (CurrentChannel.states.Count == 0)
                return null;

            // Check for past clip end
            if (offset > Duration)
                return null;
            

            // Default to first frame
            ReplaySnapshot current = CurrentChannel.states[0];

            // Check all states to find the best matching snapshot that has a time stamp greater than the specified offset (Always snap to newer frame)
            foreach(ReplaySnapshot snapshot in CurrentChannel.states)
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
        /// Prepare the <see cref="ActiveChannel"/>. 
        /// Clears all state information for the current recording essentially restoring the memory target to its initial state.
        /// </summary>
        public override void PrepareTarget(ReplayTargetTask mode)
        {
            // Only listen for discard events - We can switch between read and write instantly
            if (mode == ReplayTargetTask.Discard)
            {
                // Clear all recorded data
                CurrentChannel.states.Clear();
                duration = 0;
            }
            else if (mode == ReplayTargetTask.Commit)
            {
                // Modify the snapshot time stamps so that the recording is 0 offset based
                if(CurrentChannel.states.Count > 0)
                {
                    // Get the timestamp of the first frame
                    float offsetTime = CurrentChannel.states[0].TimeStamp;

                    // Deduct the time stamp from all other frames to make them offset based on 0 time
                    foreach (ReplaySnapshot snapshot in CurrentChannel.states)
                        snapshot.CorrectTimestamp(-offsetTime);
                }
            }
            else if(mode == ReplayTargetTask.PrepareWrite)
            {
#if UNITY_5_3_OR_NEWER
                // Get the current scene name
                ReplayMemoryChannel active = CurrentChannel;
                {
                    // Set the scene name
                    active.sceneName = SceneManager.GetActiveScene().name;
                }
                CurrentChannel = active;
#else
                // Get the current scene name
                ReplayMemoryChannel active = CurrentChannel;
                {
                    // Set the scene name
                    active.sceneName = Application.loadedLevelName;
                }
                CurrentChannel = active;
#endif
            }
        }

        /// <summary>
        /// Attempt to set the number of channels for this target.
        /// This may cause the number of channels to grow or shrink.
        /// Note that there must always be atleast <see cref="minNumberOfChannels"/>. 
        /// Note that if the active channel is removed as a result of this operation, then the acitve channel will become '0'.
        /// </summary>
        /// <param name="amount">The number of channels required</param>
        /// <exception cref="InvalidOperationException">The operation would cause the number of channels to become lower than <see cref="minNumberOfChannels"/></exception>
        /// <exception cref="InvalidOperationException">The replay system is currently recording</exception>
        public void SetNumberOfChannels(int amount)
        {
            // Check we are not recording
            if (IsRecording == true)
                throw new InvalidOperationException("You cannot modify the number of channels during recording");

            if(amount > channels.Count)
            {
                // Add channels
                while(channels.Count < amount)
                {
                    // Add channel to the back
                    AddChannel();
                }
            }
            else if(amount < channels.Count)
            { 
                // Remove channels
                while(channels.Count > amount)
                {
                    // Remove a channel from the back
                    RemoveChannel();

                    // Validate the current channel - reset if necessary
                    if (HasChannel(activeChannel) == false)
                        activeChannel = 0;
                }
            }            
        }

        /// <summary>
        /// Attempt to set the active channel for the target.
        /// All further operations will be performed on the active channel.
        /// </summary>
        /// <param name="channel">The channel index to make active</param>
        /// <exception cref="IndexOutOfRangeException">The specified channel does not exist in the target</exception>
        /// <exception cref="InvalidOperationException">The replay system is currently recording</exception>
        public void SetActiveChannel(int channel)
        {
            // Check for recording
            if (IsRecording == true)
                throw new InvalidOperationException("You cannot change the active channel during recording");

            // Validate index
            if (HasChannel(channel) == false)
                throw new IndexOutOfRangeException("'channel' must map to a valid channel index");

            // Set the active channel
            activeChannel = channel;
        }

        /// <summary>
        /// Adds a new channel to the target.
        /// The channel index of this new channel can be considered as <see cref="ChannelCount"/> - 1. 
        /// IE: the channel is added to the back.
        /// </summary>
        /// <param name="makeActive">Should the new channel be set as the active channel</param>
        public void AddChannel(bool makeActive = true)
        {
            // Create a new channel
            channels.Add(new ReplayMemoryChannel
            {
                states = new List<ReplaySnapshot>(),
                initialStateBuffer = new ReplayInitialDataBuffer(),
                sceneName = string.Empty,
            });

            // Make the channel active
            if (makeActive == true)
                SetActiveChannel(ChannelCount - 1);
        }

        /// <summary>
        /// Attempts to remove the specified channel from the target.
        /// If '-1' is specified as the channel then the last channel (<see cref="ChannelCount"/> -1) will be removed. 
        /// <param name="channel">The channel index to remove</param>
        /// </summary>
        /// <exception cref="InvalidOperationException">The operation would cause the <see cref="ChannelCount"/> to fall below <see cref="minNumberOfChannels"/></exception>
        public void RemoveChannel(int channel = -1)
        {
            // Default to the last channel in the list
            if (channel < 0)
                channel = channels.Count - 1;

            // Make sure we keep the minimum amount of channels
            if (channels.Count <= minNumberOfChannels)
                throw new InvalidOperationException("A ReplayMultichannelMemoryTarget must have atleast 1 channel. The operation would cause all channels to be removed");

            // Remove end channel
            channels.RemoveAt(channel);
        }

        /// <summary>
        /// Checks whether the target has a channel at the specified channel index.
        /// </summary>
        /// <param name="channel">The index of the channel to check</param>
        /// <returns>True if the target has the channel or false if not</returns>
        public bool HasChannel(int channel)
        {
            // Validate index
            if(channel < channels.Count &&
                channel >= 0)
            {
                // We have ther channel
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to discard the recorded data for the specified channel.
        /// If '-1' is specified as the channel index then the <see cref="ActiveChannel"/> will be discarded.
        /// Note that discard events will still be sent to the active replay channel by the <see cref="ReplayManager"/> during recording. 
        /// </summary>
        /// <param name="channel">The channel index to discard or '-1' if the active channel should be used</param>
        /// <exception cref="IndexOutOfRangeException">The specified channel does not exist in the target</exception>
        public void DiscardChannel(int channel = -1)
        {
            // Default to the active channel
            if (channel < 0)
                channel = activeChannel;

            // Store the active channel
            int tempActive = activeChannel;

            // Switch to the channel temporarily
            SetActiveChannel(channel);

            // Send a replay discard event
            PrepareTarget(ReplayTargetTask.Discard);

            // Switch back to our old target
            SetActiveChannel(tempActive);
        }

        /// <summary>
        /// Attempts to discard the recorded data for all channels in this target.
        /// Note that discard events will still be sent to the active replay channel by the <see cref="ReplayManager"/> during recording. 
        /// </summary>
        public void DiscardChannels()
        {
            // Discard all channels
            for(int i = 0; i < channels.Count; i++)
            {                
                // Discard the channel
                DiscardChannel(i);
            }
        }

        private void ConstrainBuffer()
        {
            // Check for unlimited buffer
            if (recordSeconds == 0)
                return;

            // Make sure we have more than one state
            if (CurrentChannel.states.Count == 0)
                return;

            // Keep an additional .2 of a second to ensure we have atleast the requested amount of recording
            float timeCompensation = 0.2f;

            // Calculate the start time
            float targetStartTime = CurrentChannel.states[CurrentChannel.states.Count - 1].TimeStamp - (recordSeconds + timeCompensation);

            // Process all buffered frames
            for (int i = 0; i < CurrentChannel.states.Count; i++)
            {
                // Check for any expired frames
                if (CurrentChannel.states[i].TimeStamp <= targetStartTime)
                {
                    // The frame is too old so we can throw it away
                    CurrentChannel.states.RemoveAt(i);
                }
            }
        }
    }
}
