using UnityEngine;

namespace UltimateReplay.Core
{
    internal sealed class ReplayTimer
    {
        // Private
        private static float systemTimer = 0;

        private float startTime = -float.MaxValue;
        private float interval = 1;

        // Properties
        public float Interval
        {
            get { return interval; }
            set { interval = value; }
        }

        public float ElapsedSeconds
        {
            get { return systemTimer - startTime; }
        }

        // Constructor
        public ReplayTimer() { }

        public ReplayTimer(float interval)
        {
            this.interval = interval;
        }

        // Methods
        public static void Tick(float deltaTime)
        {
            systemTimer += deltaTime;

            //if (fixedTime == true)
            //{
            //    // Add fixed delta time
            //    systemTimer += Time.fixedDeltaTime;
            //}
            //else
            //{
            //    // Add delta time
            //    systemTimer += Time.deltaTime;
            //}
        }

        public bool HasElapsed()
        {
            // Check if our target time has passed
            return HasElapsed(interval);
        }

        public bool HasElapsed(float time)
        {
            // Check if enough time has passed
            if(systemTimer >= (startTime + time))
            {
                Reset();
                return true;
            }
            return false;
        }
        
        public void Reset()
        {
            // Reset our timer
            startTime = systemTimer;
        }
    }
}
