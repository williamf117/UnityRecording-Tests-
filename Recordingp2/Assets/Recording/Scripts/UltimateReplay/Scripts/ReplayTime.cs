
namespace UltimateReplay
{
    /// <summary>
    /// This class emulates the behaviour of the Time class in Unity and can be used to modify the playback speed of a replay.
    /// There are also delta values that can be used to interpolate between frames where a low record frame rate is used. See <see cref="ReplayTransform"/> for an example. 
    /// </summary>
    public static class ReplayTime
    {
        // Private
        private static float time = 0;
        private static float delta = 0;
        private static float timeScale = 1f;

        // Properties
        /// <summary>
        /// Get the current replay playback time.
        /// </summary>
        public static float Time
        {
            get { return time; }
            internal set { time = value; }
        }

        /// <summary>
        /// Represents a delta between current replay frames.
        /// This normalized value can be used to interpolate smoothly between replay states where a low record rate is used.
        /// Note: this value is not the actual delta time but a value representing the transition progress between replay frames.
        /// </summary>
        public static float Delta
        {
            get { return delta; }
            internal set { delta = value; }
        }

        /// <summary>
        /// The time scale value used during replay playback. 
        /// You can set this value to negative values to control the direction of playback.
        /// This value is ignored during replay recording.
        /// </summary>
        public static float TimeScale
        {
            get { return timeScale; }
            set { timeScale = value; }
        }

        /// <summary>
        /// Get the playback direction based on the current <see cref="TimeScale"/> value. 
        /// </summary>
        public static PlaybackDirection TimeScaleDirection
        {
            get
            {
                if (timeScale < 0)
                    return PlaybackDirection.Backward;

                return PlaybackDirection.Forward;
            }
        }

        // Methods
        /// <summary>
        /// Causes the <see cref="TimeScale"/> value to be reset to its default value of '1'. 
        /// </summary>
        public static void ResetTimeScale()
        {
            timeScale = 1f;
        }

        /// <summary>
        /// Gets the current time as a float and converts it to minutes and seconds formatted as a string.
        /// </summary>
        /// <param name="timeValue">The time value input, for example: Time.time</param>
        /// <returns>A formatted time string</returns>
        public static string GetCorrectedTimeValueString(float timeValue)
        {
            int minutes = (int)(timeValue / 60);
            int seconds = (int)(timeValue % 60);

            return string.Format("{0}:{1}", minutes, seconds.ToString("00"));
        }
    }
}
