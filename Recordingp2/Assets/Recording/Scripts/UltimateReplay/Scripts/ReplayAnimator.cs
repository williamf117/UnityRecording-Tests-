using System.Collections.Generic;
using UnityEngine;

// To be fully released in a future version


namespace UltimateReplay
{
    /// <summary>
    /// Used to store animator state data between one or two replay frames for interpolation.
    /// </summary>
    internal struct ReplayAnimatorState
    {
        // Public
        /// <summary>
        /// The hash of the last animation clip to play.
        /// </summary>
        public int lastHash;
        /// <summary>
        /// The hash of the target animation clip to play.
        /// </summary>
        public int targetHash;
        /// <summary>
        /// The last normalized time in the animation clip.
        /// </summary>
        public float lastNormalizedTime;
        /// <summary>
        /// The target normalized time in the animation clip.
        /// </summary>
        public float targetNormalizedTime;
    }

    /// <summary>
    /// Attatch this component to a game object in order to record the objects <see cref="Animator"/> component for replays.
    /// Only one instance of <see cref="ReplayAnimator"/> can be added to any game object. 
    /// </summary>
    [DisallowMultipleComponent]
    public class ReplayAnimator : ReplayBehaviour
    {
        // Private
        private ReplayAnimatorState[] animatorStates = new ReplayAnimatorState[0];
        private float cachedSpeed = 0;

        // Public
        /// <summary>
        /// The animator component that will be recorded by thie <see cref="ReplayAnimator"/>. 
        /// </summary>
        public Animator observedAnimator;

        /// <summary>
        /// Should the animator be interpolated during playback.
        /// </summary>
        public bool interpolate = true;

        /// <summary>
        /// Should all the animator layers be recorded. When false, only the default animator layer (0) will be recorded.
        /// </summary>
        public bool recordAllLayers = true;

        // Methods
        /// <summary>
        /// Called by Unity.
        /// </summary>
        public override void Awake()
        {
            // Check for source
            if(observedAnimator == null)
            {
                Debug.LogWarningFormat("No animator source for '{0}' component, {1}", GetType().Name, gameObject.name);
                return;
            }

            // Get the current speed
            cachedSpeed = observedAnimator.speed;

            // Call the base method
            base.Awake();
        }

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public override void OnEnable()
        {
            // Call the base method
            base.OnEnable();
        }

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public override void OnDisable()
        {
            // Call the base method
            base.OnDisable();
        }

        /// <summary>
        /// Called by the replay system when the state should be reset.
        /// </summary>
        public override void OnReplayReset()
        {
            for(int i = 0; i < animatorStates.Length; i++)
            {
                // Make sure the last and target are the same
                animatorStates[i].lastHash = animatorStates[i].targetHash;
                animatorStates[i].lastNormalizedTime = animatorStates[i].targetNormalizedTime;
            }
        }

        /// <summary>
        /// Called by the replay system when playback begins.
        /// </summary>
        public override void OnReplayStart()
        {
            // Check for animator
            if (observedAnimator == null)
                return;

            // Disable animator playback - use fixed frame stepping
            cachedSpeed = observedAnimator.speed;
            observedAnimator.speed = 0;
        }

        /// <summary>
        /// Called by the replay system when playback ends.
        /// </summary>
        public override void OnReplayEnd()
        {
            // Check for animator
            if (observedAnimator == null)
                return;

            // Restore the animator speed
            observedAnimator.speed = cachedSpeed;
        }

        public override void OnReplayPlayPause(bool paused)
        {
            // Check for animator
            if (observedAnimator == null)
                return;

            if(paused == true)
            {
                // Stop animating
                observedAnimator.speed = 0f;
            }
            else
            {
                // Resume animating
                observedAnimator.speed = cachedSpeed;
            }
        }

        /// <summary>
        /// Called during playback and allows the animator to be interpolated to provide a smooth replay even if lower record rates are used.
        /// </summary>
        public override void OnReplayUpdate()
        {
            // Check for animator
            if (observedAnimator == null)
                return;

            // Make sure the animator is enabled for playback
            observedAnimator.enabled = true;

            // be sure to update all states
            for (int i = 0; i < animatorStates.Length; i++)
            {
                // Default to target time
                float normalizedTime = animatorStates[i].targetNormalizedTime;

                // Check for interpolation
                if (interpolate == true && animatorStates[i].lastHash == animatorStates[i].targetHash)
                {
                    // Interpolate the animator 
                    normalizedTime = Mathf.Lerp(animatorStates[i].lastNormalizedTime, normalizedTime, ReplayTime.Delta);
                }

                // Set the animator frame
                observedAnimator.Play(animatorStates[i].targetHash, i, normalizedTime);
            }
        }

        /// <summary>
        /// Called by the replay system when the object should be recorded.
        /// </summary>
        /// <param name="state">The state object to serialize the animator into</param>
        public override void OnReplaySerialize(ReplayState state)
        {
            // Check for animator
            if (observedAnimator == null)
                return;

            // Calcualte the number of layers we want to serialize
            int layerCount = (recordAllLayers == true) ? observedAnimator.layerCount : 1;

            //// Write the layer count
            state.Write(layerCount);

            // Serialize all layer info
            for (int i = 0; i < layerCount; i++)
            {
                // Get the current animator state
                AnimatorStateInfo info = observedAnimator.GetCurrentAnimatorStateInfo(i);

                // Get the normalized time
                float normalizedTime = info.normalizedTime;

                // Store the state information
                state.Write(info.fullPathHash);
                state.Write(normalizedTime);
            }
        }

        /// <summary>
        /// Called by the replay system when the object should return to a previous state.
        /// </summary>
        /// <param name="state">The state object to deserialize the animator from</param>
        public override void OnReplayDeserialize(ReplayState state)
        {
            // Check for animator
            if (observedAnimator == null)
                return;

            // Read the number of layers
            int layerCount = state.Read32();

            // Check if we need to reallocate
            if (layerCount > animatorStates.Length)
                animatorStates = new ReplayAnimatorState[layerCount];

            // Read all layer information
            for (int i = 0; i < layerCount; i++)
            {
                // Update last values
                animatorStates[i].lastHash = animatorStates[i].targetHash;
                animatorStates[i].lastNormalizedTime = animatorStates[i].targetNormalizedTime;

                // Deserialize animator values
                animatorStates[i].targetHash = state.Read32();
                animatorStates[i].targetNormalizedTime = state.ReadFloat();
            }

            // Check for paused
            if (ReplayManager.IsPaused == true)
                observedAnimator.speed = 0;
        }
    }
}
