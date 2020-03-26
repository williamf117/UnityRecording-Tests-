using System;

namespace UltimateReplay.Storage
{
    /// <summary>
    /// Represents a task that can be issued to a <see cref="ReplayTarget"/>.
    /// </summary>
    public enum ReplayTargetTask
    {
        /// <summary>
        /// The replay target should commit all data currently in memeory to its end destination.
        /// Similar to a flush method.
        /// </summary>
        Commit,
        /// <summary>
        /// The replay target should discard any recorded data.
        /// </summary>
        Discard,
        /// <summary>
        /// The replay target should prepare for subsequent write requests.
        /// </summary>
        PrepareWrite,
        /// <summary>
        /// The replay target should prepare for subsequent read requests.
        /// </summary>
        PrepareRead,
    }

    /// <summary>
    /// Represents and abstract storage device capable of holding recorded state data for playback at a later date.
    /// Depending upon implementation, a <see cref="ReplayTarget"/> may be volatile or non-volatile. 
    /// </summary>
    [Serializable]
    [ReplayIgnore]
    public abstract class ReplayTarget : ReplayBehaviour
    {
        // Protected
        /// <summary>
        /// The amount of time in seconds that the current recording data lasts.
        /// If no data exists then the duration will dfault to a length of 0.
        /// </summary>
        protected float duration = 0;

        // Properties
        /// <summary>
        /// The amount of time in seconds that this recording lasts.
        /// </summary>
        public abstract float Duration { get; }

        /// <summary>
        /// Get the total amount of bytes that this replay uses.
        /// </summary>
        public abstract int MemorySize { get; }

        /// <summary>
        /// Get the initial state buffer for the replay target. The state buffer is essential for storing dynamic object information.
        /// </summary>
        public abstract ReplayInitialDataBuffer InitialStateBuffer { get; }

        /// <summary>
        /// Get the name of the scene that the recording was taken from.
        /// </summary>
        public abstract string TargetSceneName { get; }        

        // Methods
        /// <summary>
        /// Store a replay snapshot in the replay target.
        /// </summary>
        /// <param name="state">The snapshot to store</param>
        public abstract void RecordSnapshot(ReplaySnapshot state);

        /// <summary>
        /// Recall a snapshot from the replay target based on the specified replay offset.
        /// </summary>
        /// <param name="offset">The offset pointing to the individual snapshot to recall</param>
        /// <returns>The replay snapshot at the specified offset</returns>
        public abstract ReplaySnapshot RestoreSnapshot(float offset);
        
        /// <summary>
        /// Called by the recording system to notify the active <see cref="ReplayTarget"/> of an upcoming event. 
        /// </summary>
        /// <param name="mode">The <see cref="ReplayTargetTask"/> that the target should prepare for</param>
        public abstract void PrepareTarget(ReplayTargetTask mode);
    }
}
