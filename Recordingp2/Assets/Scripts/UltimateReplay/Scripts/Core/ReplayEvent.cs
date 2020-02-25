
namespace UltimateReplay.Core
{
    /// <summary>
    /// Common events identifiers used to record <see cref="ReplayEvent"/> with the replay system. 
    /// </summary>
    public enum ReplayEvents : byte
    {
        /// <summary>
        /// Object instantiated event.
        /// </summary>
        ObjectSpawn = 1,
        /// <summary>
        /// Object destroyed event.
        /// </summary>
        ObjectDespawn,
        /// <summary>
        /// Play audio clip event.
        /// </summary>
        PlaySound,
        /// <summary>
        /// Play audio music event.
        /// </summary>
        PlayMusic,
        /// <summary>
        /// Start particle system event.
        /// </summary>
        ParticleStart,
        /// <summary>
        /// Stop particle system event.
        /// </summary>
        ParticleEnd,
    }

    /// <summary>
    /// Used to mark key recording events that will be triggered during playback.
    /// Good candidates would be to trigger audio effects or similar.
    /// </summary>
    public struct ReplayEvent : IReplaySerialize
    {
        // Public
        /// <summary>
        /// A unique event identifier used to distinguish between different replay events.
        /// </summary>
        public byte eventID;
        /// <summary>
        /// The replay state data associated with this <see cref="ReplayEvent"/>. 
        /// The event data should contain no more than 255 bytes to ensure that the data is serialized correctly.
        /// </summary>
        public ReplayState eventData;

        // Methods
        /// <summary>
        /// Called by the replay system when all replay state information should be serialized.
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to write the data to</param>
        public void OnReplaySerialize(ReplayState state)
        {
            // Write the id
            state.Write(eventID);

            // Get the data size
            byte size = (byte)eventData.Size;

            // Write the size and data
            state.Write(size);
            state.Write(eventData);
        }

        /// <summary>
        /// Called by the replay system when all replay state information should be deserialized.
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to read the data from</param>
        public void OnReplayDeserialize(ReplayState state)
        {
            // Read the id
            eventID = state.ReadByte();

            // Read the size
            byte size = state.ReadByte();

            // Read the data
            eventData = state.ReadState(size);
        }
    }
}
