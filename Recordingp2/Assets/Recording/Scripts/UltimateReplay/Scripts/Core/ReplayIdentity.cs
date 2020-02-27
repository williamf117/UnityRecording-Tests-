#define ReplayIdentityBit_16

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateReplay.Core
{
    /// <summary>
    /// A replay identity is an essential component in the Ultimate Replay system and is used to identify replay objects between sessions.
    /// Replay identities are assigned at edit time where possible and will never change values.
    /// Replay identities are also use to identify prefab instances that are spawned during a replay.
    /// </summary>
    [Serializable]
    public sealed class ReplayIdentity : IEquatable<ReplayIdentity>
    {
        // Internal
        internal const int maxGenerateAttempts = 512;
        internal const int unassignedID = -1;

        // Private
        private static List<ReplayIdentity> usedIds = new List<ReplayIdentity>();

#if ReplayIdentityBit_16
        [SerializeField]
        private short id = unassignedID;
#else
        [SerializeField]
        private int id = unassignedID;
#endif

        // Public
        /// <summary>
        /// Get the number of bytes that this object uses to represent its id data.
        /// </summary>
#if ReplayIdentityBit_16
        public static readonly int byteSize = sizeof(short);
#else
        public static readonly int byteSize = sizeof(int);
#endif

        // Properties
#if UNITY_EDITOR
        /// <summary>
        /// Enumerates all used <see cref="ReplayIdentity"/> objects in the domain. 
        /// used for debugging only.
        /// </summary>
        public static IEnumerable<ReplayIdentity> Identities
        {
            get { return usedIds; }
        }
#endif

        /// <summary>
        /// Returns true if this id is not equal to <see cref="unassignedID"/>. 
        /// </summary>
        private bool IsGenerated
        {
            get { return id != unassignedID; }
        }

        // Constructor
        /// <summary>
        /// Clear any old data on domain reload.
        /// </summary>
        static ReplayIdentity()
        {
            // Clear the set - it will be repopulated when each identity is initialized
            usedIds.Clear();
        }

        public ReplayIdentity() { }

        /// <summary>
        /// Create a new instance with the specified id value.
        /// </summary>
        /// <param name="id">The id value to give to this identity</param>
        public ReplayIdentity(short id)
        {
#if ReplayIdentityBit_16
            // Store the id
            this.id = id;
#else
            this.id = (int)id;
#endif
        }

        /// <summary>
        /// Create a new instance with the specified id value.
        /// </summary>
        /// <param name="id">The id value to give this identity</param>
        public ReplayIdentity(int id)
        {
#if ReplayIdentityBit_16
            // Store the id
            this.id = (short)id;
#else
            this.id  = id;
#endif
        }

        // Methods
        #region InheritedAndOperator
        /// <summary>
        /// Override implementation.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }

        /// <summary>
        /// Override implementation.
        /// </summary>
        /// <param name="obj">The object to compare against</param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            // Check for null
            if (obj == null)
                return false;

            // Check for type
            ReplayIdentity id = obj as ReplayIdentity;

            // Check for error
            if (id == null)
                return false;

            // Call through
            return Equals(id);
        }

        /// <summary>
        /// IEquateable implementation.
        /// </summary>
        /// <param name="obj">The <see cref="ReplayIdentity"/> to compare against</param>
        /// <returns></returns>
        public bool Equals(ReplayIdentity obj)
        {
            // Check for null
            if (obj == null)
                return false;

            // Compare values
            return id == obj.id;
        }

        /// <summary>
        /// Override implementation.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("ReplayIdentity({0})", id);
        }

        /// <summary>
        /// Override equals operator.
        /// </summary>
        /// <param name="a">First <see cref="ReplayIdentity"/></param>
        /// <param name="b">Second <see cref="ReplayIdentity"/></param>
        /// <returns></returns>
        public static bool operator ==(ReplayIdentity a, ReplayIdentity b)
        {
            // Support for null compare
            if (ReferenceEquals(a, null) == true)
                return ReferenceEquals(b, null);

            if (object.Equals(a, b) == true)
                return true;

            // Check for equal
            return a.Equals(b) == true;
        }

        /// <summary>
        /// Override not-equals operator.
        /// </summary>
        /// <param name="a">First <see cref="ReplayIdentity"/></param>
        /// <param name="b">Second <see cref="ReplayIdentity"/></param>
        /// <returns></returns>
        public static bool operator !=(ReplayIdentity a, ReplayIdentity b)
        {
            // Support for null compare
            if (ReferenceEquals(a, null) == true)
                return ReferenceEquals(b, null) == false;

            // Check for not equal
            return a.Equals(b) == false;
        }

#if ReplayIdentityBit_16
        /// <summary>
        /// Implcity conversion to int16.
        /// </summary>
        /// <param name="identity">The identity to convert</param>
        public static implicit operator short(ReplayIdentity identity)
#else
        public static implicit operator int(ReplayIdentity identity)        
#endif
        { 
            // Get short identity
            return identity.id;
        }
#endregion

        public static void RegisterIdentity(ReplayIdentity identity)
        {
            // Generate a unique id
            if (IsUnique(identity) == false)
                Generate(identity);

            // Register the id
            if(usedIds.Contains(identity) == false)
                usedIds.Add(identity);
        }

        public static void UnregisterIdentity(ReplayIdentity identity)
        {
            // Remove the id
            if (usedIds.Contains(identity) == true)
                usedIds.Remove(identity);
        }


        private static void Generate(ReplayIdentity identity)
        {
#if ReplayIdentityBit_16
            short next = unassignedID;
            short count = 0;

            // Use 2 byte array to create 16 bit int
            byte[] buffer = new byte[2];

#else
            int next = unassignedID;
            int count = 0;
#endif

            // Create a random instance
            System.Random random = new System.Random((int)DateTime.Now.Ticks);
            
            do
            {
                // Check for long loop
                if (count > maxGenerateAttempts)
                    throw new OperationCanceledException("Attempting to find a unique replay id took too long. The operation was canceled to prevent a long or infinite loop");
                
#if ReplayIdentityBit_16
                // Randomize the buffer
                random.NextBytes(buffer);

                // Use random instead of linear
                next = (short)(buffer[0] << 8 | buffer[1]);
#else
                // Get random int
                next = random.Next();
#endif

                // Keep track of how many times we have tried
                count++;
            }
            // Make sure our set does not contain the id
            while (next == unassignedID ||  IsValueUnique(next) == false);

            // Update identity with unique value
            identity.id = next;     
        }

#if ReplayIdentityBit_16
        private static bool IsValueUnique(short value)
#else
        private static bool IsValueUnique(int value)
#endif
        {
            foreach (ReplayIdentity used in usedIds)
                if (used.id == value)
                    return false;

            return true;
        }

        private static bool IsUnique(ReplayIdentity identity)
        {
            if (identity.IsGenerated == false)
                return false;

            foreach(ReplayIdentity used in usedIds)
            {
                if(ReferenceEquals(used, identity) == false)
                    if (used.id == identity.id)
                        return false;
            }

            return true;
        }
    }
}
