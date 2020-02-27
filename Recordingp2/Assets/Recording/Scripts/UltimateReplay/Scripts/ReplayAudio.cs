using UltimateReplay.Core;
using UnityEngine;

namespace UltimateReplay
{
    /// <summary>
    /// A replay component that is responsible for recording and replaying audio effects that are played during gameplay.
    /// Audio can only be replayed when playback is not being reversed.
    /// </summary>
    public class ReplayAudio : ReplayBehaviour
    {
        // Public
        /// <summary>
        /// The audio source that will be recorded by this <see cref="ReplayAudio"/>. 
        /// </summary>
        public AudioSource observedAudioSource;

        // Methods
        /// <summary>
        /// Called by Unity.
        /// </summary>
        public override void Awake()
        {
            // Check for source
            if(observedAudioSource == null)
            {
                Debug.LogWarningFormat("No audio source for '{0}' component, '{1}'", GetType().Name, gameObject.name);
                return;
            }

            // Call the base method
            base.Awake();
        }

        /// <summary>
        /// You should call this method as a replacement for AudioSource.Play as it will also record the time of the audio event so that it can be replayed later.
        /// </summary>
        public void Play()
        {
            // Require valid source
            if (observedAudioSource != null)
            {
                // Play the audio source as usual
                observedAudioSource.Play();

                // Make sure we are recording - Record the audio event if so
                if (IsRecording == true)
                {
                    // Record the event
                    ReplayRecordEvent(ReplayEvents.PlaySound);
                }
            }
        }

        /// <summary>
        /// Called by the replay system when a replay event has occured.
        /// </summary>
        /// <param name="replayEvent">The <see cref="ReplayEvent"/> that was triggered</param>
        public override void OnReplayEvent(ReplayEvent replayEvent)
        {
            switch((ReplayEvents)replayEvent.eventID)
            {
                // Listen for sound event
                case ReplayEvents.PlaySound:
                    {
                        // We cannot replay sounds during reverse playback
                        if (PlaybackDirection == PlaybackDirection.Backward)
                            break;

                        // Require valid source
                        if (observedAudioSource != null)
                        {
                            // Set the audio source to play at the playback speed
                            observedAudioSource.pitch = ReplayTime.TimeScale;

                            // Play the audio
                            observedAudioSource.Play();
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// Called by the replay system every frame while playback is active.
        /// </summary>
        public override void OnReplayUpdate()
        {
            // We cannot replay sounds during reverse playback
            if (PlaybackDirection == PlaybackDirection.Backward)
                return;

            // Check for valid audio
            if (observedAudioSource != null)
            {
                // Update the time scale every frame because it can be changed at any time - the audio source should respond immediatley.
                if (observedAudioSource.isPlaying == true)
                    observedAudioSource.pitch = ReplayTime.TimeScale;
            }
        }

        public override void OnReplayEnd()
        {
            // Dont allow sound to run into the game
            if (observedAudioSource != null)
                observedAudioSource.Stop();
        }
    }
}
