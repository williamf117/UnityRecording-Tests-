using UnityEngine;
using UltimateReplay.Storage;

namespace UltimateReplay.Core
{
    internal enum ReplaySequenceResult
    {
        SequenceIdle = 0,
        SequenceAdvance,
        SequenceEnd,
    }

    internal sealed class ReplaySequencer
    {
        // Private
        private ReplayTarget target = null;
        private ReplaySnapshot current = null;
        private ReplaySnapshot last = null;
        private float playbackTime = 0;

        // Properties
        public ReplayTarget Target
        {
            set { target = value; }
        }

        public float CurrentTime
        {
            get { return playbackTime; }
        }

        public float CurrentTimeNormalized
        {
            get
            {
                // Remap to 0-1
                return MapScale(playbackTime, 0, target.Duration, 0, 1);
            }
        }
        
        // Methods
        public ReplaySnapshot SeekPlayback(float offset, PlaybackOrigin origin, bool normalized)
        {
            // Check for normalized
            if (normalized == false)
            {
                // Check for seek mode
                switch (origin)
                {
                    case PlaybackOrigin.Start:
                        playbackTime = offset;
                        break;

                    case PlaybackOrigin.End:
                        playbackTime = target.Duration - offset;
                        break;

                    case PlaybackOrigin.Current:
                        playbackTime += offset;
                        break;
                }
            }
            else
            {
                // Clamp the input valid
                offset = Mathf.Clamp01(offset);

                // Check for seek mode
                switch(origin)
                {
                    case PlaybackOrigin.Start:
                        playbackTime = MapScale(offset, 0, 1, 0, target.Duration);
                        break;

                    case PlaybackOrigin.End:
                        playbackTime = MapScale(offset, 1, 0, 0, target.Duration);
                        break;

                    case PlaybackOrigin.Current:
                        playbackTime = MapScale(offset, 0, 1, playbackTime, target.Duration);
                        break;
                }
            }

            // Clamp to valid range
            playbackTime = Mathf.Clamp(playbackTime, 0, target.Duration);

            // Restore the scene state
            current = target.RestoreSnapshot(playbackTime);

            // Check for change
            if(current != last)
            {
                // Update the replay time
                ReplayTime.Time = playbackTime;

                if (last != null && current != null)
                {
                    // Check for backwards
                    if (last.TimeStamp <= current.TimeStamp)
                    {
                        // Forward
                        ReplayTime.Delta = MapScale(playbackTime, last.TimeStamp, current.TimeStamp, 0, 1);
                    }
                    else
                    {
                        // Backward
                        ReplayTime.Delta = -MapScale(playbackTime, last.TimeStamp, current.TimeStamp, 1, 0);
                    }
                }
            }
            //else
            //{
            //    ReplayTime.Delta = 0;
            //}

            // Store current frame
            last = current;

            return current;
        }

        public ReplaySequenceResult UpdatePlayback(out ReplaySnapshot frame, PlaybackEndBehaviour endBehaviour, float deltaTime)
        {
            PlaybackDirection direction = ReplayTime.TimeScaleDirection;

            // Default to idle
            ReplaySequenceResult result = ReplaySequenceResult.SequenceIdle;

            if (last != null)
            {
                if (direction == PlaybackDirection.Forward)
                {
                    // Calculatet the delta time
                    ReplayTime.Delta = MapScale(playbackTime, last.TimeStamp, current.TimeStamp, 0, 1);
                }
                else
                {
                    // Calculate the delta for reverse playback
                    ReplayTime.Delta = -MapScale(playbackTime, current.TimeStamp, last.TimeStamp, 0, 1);
                }
            }
            else
            {
                if (current == null)
                {
                    ReplayTime.Delta = 0;
                }
                else
                {
                    ReplayTime.Delta = MapScale(playbackTime, 0, current.TimeStamp, 0, 1);
                }
            }

            // Clamp delta
            ReplayTime.Delta = Mathf.Clamp01(ReplayTime.Delta);

            float delta = (deltaTime * ReplayTime.TimeScale);

            //if (fixedTime == true)
            //{
            //    // Find the fixed time delta
            //    delta = (Time.fixedDeltaTime * ReplayTime.TimeScale);
            //}
            //else
            //{
            //    // Find the time delta
            //    delta = (Time.deltaTime * Mathf.Abs(ReplayTime.TimeScale));
            //}

            // Advance our frame
            switch (direction)
            {
                case PlaybackDirection.Forward:
                    {
                        playbackTime += delta;
                    }
                    break;

                case PlaybackDirection.Backward:
                    {
                        playbackTime -= delta;
                    }
                    break;
            }

            switch (endBehaviour)
            {
                default:
                case PlaybackEndBehaviour.EndPlayback:
                    {
                        // Check for end of playback
                        if (playbackTime >= target.Duration || playbackTime < 0)
                        {
                            frame = null;
                            return ReplaySequenceResult.SequenceEnd;
                        }
                        break;
                    }

                case PlaybackEndBehaviour.LoopPlayback:
                    {
                        if (playbackTime >= target.Duration || playbackTime < 0)
                        {
                            playbackTime = (direction == PlaybackDirection.Forward) ? 0 : target.Duration;
                        }
                        break;
                    }

                case PlaybackEndBehaviour.StopPlayback:
                    {
                        if(playbackTime >= target.Duration)
                        {
                            playbackTime = target.Duration;
                        }
                        else if(playbackTime < 0)
                        {
                            playbackTime = 0;
                        }
                        break;
                    }
            }
            

            // Try to get the current frame
            ReplaySnapshot temp = target.RestoreSnapshot(playbackTime);
            
            // Check for valid frame
            if (temp != null)
            {
                // Check for sequence advancement
                if (current != temp)
                {
                    // Snap to next frame
                    ReplayTime.Delta = 0;

                    // Set the result
                    result = ReplaySequenceResult.SequenceAdvance;

                    // Update last frame
                    last = current;
                }

                // Update the current frame
                current = temp;
            }
            else
            {
                // Do nothing - We may be inbetween replay frames

                // Trigger sequence end
                //frame = null;
                //return ReplaySequenceResult.SequenceEnd;
            }

            // The sequencer only updated its timing values and there was no state change
            frame = current;
            return result;
        }

        private void UpdateTime()
        {
            // Set the playback time
            ReplayTime.Time = playbackTime;            

            //switch(direction)
            //{
            //    case PlaybackDirection.Forward:
            //        {
            //            // Find the current and next frame times
            //            float currentTime = (target.FrameStep * currentFrame);
            //            float nextTime = (target.FrameStep * (currentFrame + 1));

            //            // Map the scalar
            //            ReplayTime.Delta = ScaleTime(playbackTime, currentTime, nextTime);

            //        } break;

            //    case PlaybackDirection.Backward:
            //        {
            //            // Find the current and next frames
            //            float currentTime = (target.FrameStep * currentFrame);
            //            float nextTime = (target.FrameStep * (currentFrame - 1));

            //            // Map the scalar - next time will always be smaller than current time so be sure to specify the values in the correct order
            //            ReplayTime.Delta = ScaleTime(playbackTime, nextTime, currentFrame);
            //        } break;
            //}
        }

        private float ScaleTime(float value, float min, float max)
        {
            // Constrain values
            if (value < min) return 0;
            if (value > max) return 1;

            // Scale the time
            return (value - min) / (max - min);
        }

        private float MapScale(float value, float min, float max, float newMin, float newMax)
        {
            return newMin + (value - min) * (newMax - newMin) / (max - min);
        }
    }
}
