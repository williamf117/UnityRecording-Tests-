#if ULTIMATEREPLAY_EXPERIMENTAL == true

using UnityEngine;

namespace UltimateReplay
{
    public class ReplayParticles : ReplayBehaviour
    {
        // Private
        private float lastTime = 0;
        private float targetTime = 0;
        private bool stopParticlesOnPlaybackEnd = false;
        private bool simulateDirty = false;

        // Public
        public ParticleSystem observedParticleSystem;
        public bool simulateChildren = false;

        // Methods
        public override void Awake()
        {
            // Check for particles
            if(observedParticleSystem == null)
            {
                Debug.LogWarning(string.Format("No particle system for 'ReplayParticles' component '{0}'", gameObject.name));
                return;
            }

            Debug.Log("Note: ReplayParticles are not working in this version. An update will be released soon to fix this");
            // Call the base method
            base.Awake();
        }

        public void Play()
        {
            observedParticleSystem.Play(simulateChildren);
        }

        public void Stop()
        {
            observedParticleSystem.Stop(simulateChildren);
        }

        public override void OnReplaySerialize(ReplayState state)
        {
            // Require particle system
            if (observedParticleSystem == null)
                return;

            // Check for playing particles system - Dont waste data when nothing is happening
            //if (particles.isPlaying == false)
            //    return;

            // Write particle time
            state.Write(observedParticleSystem.time);
        }

        public override void OnReplayDeserialize(ReplayState state)
        {
            // Require particle system
            if (observedParticleSystem == null)
                return;

            // Save the last time
            lastTime = targetTime;

            // Read the target simulation time
            targetTime = state.ReadFloat();

            observedParticleSystem.Simulate(targetTime - lastTime, true, false);
        }

        public override void OnReplayStart()
        {
            // Set stop flag
            stopParticlesOnPlaybackEnd = !observedParticleSystem.isPlaying;

            // Start the particles
            if (observedParticleSystem.isPlaying == false)
                observedParticleSystem.Play(simulateChildren);
        }

        public override void OnReplayEnd()
        {
            // Stop the particles
            if (stopParticlesOnPlaybackEnd == true)
                observedParticleSystem.Stop(simulateChildren);
        }

        public override void OnReplayPlayPause(bool paused)
        {
            if(paused == true)
            {
                observedParticleSystem.Pause(simulateChildren);
            }
            else
            {
                observedParticleSystem.Play(simulateChildren);
            }
        }

        public override void OnReplayUpdate()
        {
            simulateDirty = true;
            FixedUpdate();
        }

        public void FixedUpdate()
        {
            if (simulateDirty == true)
            {

                // Interpolate time
                float current = Mathf.Lerp(lastTime, targetTime, ReplayTime.Delta);

                Debug.Log(current);

                // Scale the time
                float t = current / observedParticleSystem.duration;

                Debug.Log("t: " + t);

                //float diff = Mathf.Abs(targetTime - lastTime);

                //observedParticleSystem.time = current;

                //observedParticleSystem.Clear();
                //observedParticleSystem.Simulate(Time.deltaTime, simulateChildren, false);

                // Clear old particles
                //observedParticleSystem.Clear();

                // Go back to start
                //observedParticleSystem.time = 0;

                //observedParticleSystem.Simulate(Time.deltaTime, simulateChildren, true);

                //while(observedParticleSystem.time < t)
                //{
                //    observedParticleSystem.Simulate(Time.deltaTime, simulateChildren, false);
                //}

                //observedParticleSystem.time = current;
                //observedParticleSystem.Simulate(diff, simulateChildren, false);

                //observedParticleSystem.Play();
                //observedParticleSystem.Pause();
                // Simulate the particles based on playback time
                //observedParticleSystem.Simulate(targetTime, simulateChildren, false);
                //observedParticleSystem.Pause();


                simulateDirty = false;
            }
        }
    }
}

#endif