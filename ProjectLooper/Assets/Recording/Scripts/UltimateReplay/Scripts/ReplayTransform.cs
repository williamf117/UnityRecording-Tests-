using System;
using UnityEngine;

namespace UltimateReplay
{    
    [Flags]
    [Serializable]
    internal enum ReplayTransformFlags
    {
        LowPrecision = 1,
        Position = 2,
        Rotation = 4,
        Scale = 8,
        LocalPosition = 16,
        LocalRotation = 32,
    }

    /// <summary>
    /// Attatch this component to a game object in order to record the objects transform for replays.
    /// Only one instance of <see cref="ReplayTransform"/> can be added to any game object. 
    /// </summary>
    [DisallowMultipleComponent]
    public class ReplayTransform : ReplayBehaviour
    {
        // Types
        public enum ReplayTransformRecordSpace
        {
            None,
            Local,
            World,
        }

        // Private
        private ReplayTransformFlags targetFlags = 0;
        private Vector3 lastPosition = Vector3.zero;
        private Vector3 targetPosition = Vector3.zero;

        private Quaternion lastRotation = Quaternion.identity;
        private Quaternion targetRotation = Quaternion.identity;

        private Vector3 lastScale = Vector3.one;
        private Vector3 targetScale = Vector3.one;

        // Public
        /// <summary>
        /// Should the position value of the transform be recorded.
        /// </summary>
        //public bool recordPosition = true;
        public ReplayTransformRecordSpace recordPosition = ReplayTransformRecordSpace.Local;
        /// <summary>
        /// Should the rotation value of the transform be recorded.
        /// </summary>
        //public bool recordRotation = true;
        public ReplayTransformRecordSpace recordRotation = ReplayTransformRecordSpace.Local;
        /// <summary>
        /// Should the scale value of the transform be recorded.
        /// </summary>
        public bool recordScale = false;
        /// <summary>
        /// Should the transform be interpolated between states. 
        /// This is recommended when low record rates are used as without interpolation the playback can seem jumpy.
        /// </summary>
        public bool interpolate = true;
        /// <summary>
        /// Should the transofrm be stored using half precision.
        /// This can help to reduce the amount of recorded data but will result in reduced accuracy.
        /// </summary>
        public bool lowPrecision = false;

        // Methods
        /// <summary>
        /// Called by unity.
        /// </summary>
        public override void OnEnable()
        {
            // Be sure to call the base method
            base.OnEnable();

            // Initialize starting values
            if (recordPosition == ReplayTransformRecordSpace.Local)
            {
                lastPosition = targetPosition = transform.localPosition;
            }
            else
            {
                lastPosition = targetPosition = transform.position;
            }

            if (recordRotation == ReplayTransformRecordSpace.Local)
            {
                lastRotation = targetRotation = transform.localRotation;
            }
            else
            {
                lastRotation = targetRotation = transform.rotation;
            }

            lastScale = targetScale = transform.localScale;
        }

        /// <summary>
        /// Called when this parent replay object has been spawned during playback.
        /// </summary>
        /// <param name="position">The spawned position</param>
        /// <param name="rotation">The spawned rotation</param>
        public override void OnReplaySpawned(Vector3 position, Quaternion rotation)
        {
            // Initialize values
            lastPosition = targetPosition = position;
            lastRotation = targetRotation = rotation;
            lastScale = targetScale = transform.localScale;
        }

        /// <summary>
        /// Called when the replay should reset critical values.
        /// </summary>
        public override void OnReplayReset()
        {
            // Sync last and target
            lastPosition = targetPosition;
            lastRotation = targetRotation;
            lastScale = targetScale;
        }

        /// <summary>
        /// Called during playback and allows the transform to be interpolated to provide a smooth replay even if lower record rates are used.
        /// </summary>
        public override void OnReplayUpdate()
        {
            // Get target values
            Vector3 pos = targetPosition;
            Quaternion rot = targetRotation;
            Vector3 sca = targetScale;

            // Check for interpolation
            if(interpolate == true)
            {
                // Interpolate position
                pos = Vector3.Lerp(lastPosition, targetPosition, ReplayTime.Delta);

                // Interpolate rotation
                rot = Quaternion.Lerp(lastRotation, targetRotation, ReplayTime.Delta);

                // Interpolate scale
                sca = Vector3.Lerp(lastScale, targetScale, ReplayTime.Delta);
            }

            // Update the transform
            if ((targetFlags & ReplayTransformFlags.Position) != 0)
            {
                if ((targetFlags & ReplayTransformFlags.LocalPosition) != 0)
                {
                    transform.localPosition = pos;
                }
                else
                {
                    transform.position = pos;
                }
            }

            if ((targetFlags & ReplayTransformFlags.Rotation) != 0)
            {
                if ((targetFlags & ReplayTransformFlags.LocalRotation) != 0)
                {
                    transform.localRotation = rot;
                }
                else if (recordRotation == ReplayTransformRecordSpace.World)
                {
                    transform.rotation = rot;
                }
            }

            if ((targetFlags & ReplayTransformFlags.Scale) != 0)
            {
                transform.localScale = sca;
            }
        }

        /// <summary>
        /// Called by the replay system when the object should be recorded.
        /// </summary>
        /// <param name="state">The state object to serialize the transform into</param>
        public override void OnReplaySerialize(ReplayState state)
        {
            // Calcualte the flags
            ReplayTransformFlags flags = 0;

            // Build the flag information
            if (recordPosition != ReplayTransformRecordSpace.None)
            {
                flags |= ReplayTransformFlags.Position;

                if (recordPosition == ReplayTransformRecordSpace.Local)
                    flags |= ReplayTransformFlags.LocalPosition;
            }
            if (recordRotation != ReplayTransformRecordSpace.None)
            {
                flags |= ReplayTransformFlags.Rotation;

                if (recordRotation == ReplayTransformRecordSpace.Local)
                    flags |= ReplayTransformFlags.LocalRotation;
            }
            if (recordScale == true) flags |= ReplayTransformFlags.Scale;

            // Check for any flags
            if (flags == 0)
                return;

            // Check for low precision
            if (lowPrecision == true) flags |= ReplayTransformFlags.LowPrecision;

            // Write the flags
            state.Write((short)flags);

            // Check for low precision mode
            if (lowPrecision == false)
            {
                // Write the position
                if ((flags & ReplayTransformFlags.Position) != 0)
                {
                    if ((flags & ReplayTransformFlags.LocalPosition) != 0)
                    {
                        state.Write(transform.localPosition);
                    }
                    else
                    {
                        state.Write(transform.position);
                    }
                }

                // Write the rotation
                if ((flags & ReplayTransformFlags.Rotation) != 0)
                {
                    if ((flags & ReplayTransformFlags.LocalRotation) != 0)
                    {
                        state.Write(transform.localRotation);
                    }
                    else
                    {
                        state.Write(transform.rotation);
                    }
                }

                // Write the scale
                if ((flags & ReplayTransformFlags.Scale) != 0)
                    state.Write(transform.localScale);
            }
            else
            {
                // Write the position
                if ((flags & ReplayTransformFlags.Position) != 0)
                {
                    if ((flags & ReplayTransformFlags.LocalPosition) != 0)
                    {
                        state.WriteLowPrecision(transform.localPosition);
                    }
                    else
                    {
                        state.WriteLowPrecision(transform.position);
                    }
                }

                // Write the rotation
                if ((flags & ReplayTransformFlags.Rotation) != 0)
                {
                    if ((flags & ReplayTransformFlags.LocalRotation) != 0)
                    {
                        state.WriteLowPrecision(transform.localRotation);
                    }
                    else
                    {
                        state.WriteLowPrecision(transform.rotation);
                    }
                }

                // Write the scale
                if ((flags & ReplayTransformFlags.Scale) != 0)
                    state.WriteLowPrecision(transform.localScale);
            }
        }

        /// <summary>
        /// Called by the relay system when the object should return to a previous state.
        /// </summary>
        /// <param name="state">The state object to deserialize the transform from</param>
        public override void OnReplayDeserialize(ReplayState state)
        {
            // Update the last transform
            OnReplayReset();

            // Read the transform flags
            targetFlags = (ReplayTransformFlags)state.Read16();

            // Check for low precision mode
            if ((targetFlags & ReplayTransformFlags.LowPrecision) == 0)
            {
                // Read the position
                if ((targetFlags & ReplayTransformFlags.Position) != 0)
                    targetPosition = state.ReadVec3();

                // Read the rotation
                if ((targetFlags & ReplayTransformFlags.Rotation) != 0)
                    targetRotation = state.ReadQuat();

                // Read the scale
                if ((targetFlags & ReplayTransformFlags.Scale) != 0)
                    targetScale = state.ReadVec3();
            }
            else
            {
                // Read the position
                if ((targetFlags & ReplayTransformFlags.Position) != 0)
                    targetPosition = state.ReadVec3LowPrecision();

                // Read the rotation
                if ((targetFlags & ReplayTransformFlags.Rotation) != 0)
                    targetRotation = state.ReadQuatLowPrecision();

                // Read the scale
                if ((targetFlags & ReplayTransformFlags.Scale) != 0)
                    targetScale = state.ReadVec3LowPrecision();
            }
        }
    }
}