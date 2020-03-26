using System.Collections.Generic;
using UnityEngine;
using UltimateReplay.Core;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UltimateReplay
{
    /// <summary>
    /// Only one instance of <see cref="ReplayObject"/> can be added to any game object. 
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]    
    public sealed class ReplayObject : MonoBehaviour, IReplaySerialize
    {
        // Private
        [SerializeField]
        private ReplayIdentity replayIdentity = new ReplayIdentity();
        [SerializeField]
        [HideInInspector]
        private string prefabIdentity = string.Empty;

        private ReplayState behaviourState = new ReplayState();
        
        /// <summary>
        /// An array of <see cref="ReplayBehaviour"/> components that this object will serialize during recording.
        /// Dynamically adding replay components during recording is not supported.
        /// </summary>
        [HideInInspector]
        [SerializeField]
        private ReplayBehaviour[] observedComponents = new ReplayBehaviour[0];

        // Properties
        /// <summary>
        /// Get the unique <see cref="ReplayIdentity"/> for this <see cref="ReplayObject"/>.  
        /// </summary>
        public ReplayIdentity ReplayIdentity
        {
            get { return replayIdentity; }
            set { replayIdentity = value; }
        }

        /// <summary>
        /// Get the prefab associated with this <see cref="ReplayObject"/>. 
        /// </summary>
        public string PrefabIdentity
        {
            get { return prefabIdentity; }
        }

        /// <summary>
        /// Returns true when this game object is a prefab asset.
        /// Returns false when this game object is a scene object or prefab instance.
        /// </summary>
        public bool IsPrefab
        {
            get { return string.IsNullOrEmpty(prefabIdentity) == false; }
        }

        public IEnumerable<ReplayBehaviour> ObservedComponents
        {
            get
            {
                // Get all non-null behaviours
                foreach (ReplayBehaviour behaviour in observedComponents)
                    if (behaviour != null)
                        yield return behaviour;
            }
        }

        public int ObservedComponentsCount
        {
            get
            {
                int count = 0;
#pragma warning disable 0219
                // Count all behaviours
                foreach (ReplayBehaviour behaviour in ObservedComponents)
                    count++;
#pragma warning restore 0219

                return count;
            }
        }

        // Methods
        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void Awake()
        {
            ReplayIdentity.RegisterIdentity(replayIdentity);
            // Check if we need to generate an id
            //replayIdentity.Generate();

            
        }

        public void Update()
        {
            //if (Application.isPlaying == false)
            //    UpdatePrefabLinks();
        }

        public void OnEnable()
        {            
            if (Application.isPlaying == true)
            {
                // Register this object to be recorded
                ReplayManager.Scene.RegisterReplayObject(this);
            }
        }

        public void OnDisable()
        {
            if (Application.isPlaying == true)
            {
                // Make sure the manager is not being destroyed. If it is then we dont need to unregister because the manager will handle it.
                if (ReplayManager.IsDisposing == false)
                {
                    // Unregister this object from the replay scene
                    ReplayManager.Scene.UnregisterReplayObject(this);
                }
            }
        }

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void OnDestroy()
        {
            ReplayIdentity.UnregisterIdentity(replayIdentity);            
        }

        /// <summary>
        /// Called by Unity editor.
        /// </summary>
        public void Reset()
        {
            UpdatePrefabLinks();
        }

        /// <summary>
        /// Called by the replay system when this <see cref="ReplayObject"/> should serialize its replay data. 
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to serialize the data to</param>
        public void OnReplaySerialize(ReplayState state)
        {
            // Check for empty components
            if (observedComponents.Length == 0)
                return;

            // Always write the prefab name
            state.Write(prefabIdentity);

            // Serialize all components
            foreach (ReplayBehaviour behaviour in observedComponents)
            {
                // Check for destroyed components
                if (behaviour == null)
                    continue;

                // Clear the shared buffer
                behaviourState.Clear();

                // Serialzie the behaviour
                behaviour.OnReplaySerialize(behaviourState);

                // Check for no data - dont waste memory and time serializing nothing
                if (behaviourState.Size == 0)
                    continue;

                // Write the meta data
                state.Write(behaviour.Identity);
                state.Write((short)behaviourState.Size);

                state.Write(behaviourState);
            }
        }

        /// <summary>
        /// Called by the replay system when this <see cref="ReplayObject"/> should deserialize its replay data. 
        /// </summary>
        /// <param name="state">The <see cref="ReplayState"/> to deserialize the data from</param>
        public void OnReplayDeserialize(ReplayState state)
        {
            // Always read the prefab name
            state.ReadString();

            // Read all data
            while (state.EndRead == false)
            {
                // Read the id
                ReplayIdentity identity = state.ReadIdentity();

                // Read the size
                short size = state.Read16();

                // Mathc flag
                bool matchedData = false;

                // Check for matched id
                foreach (ReplayBehaviour behaviour in observedComponents)
                {
                    // Check for destroyed components
                    if (behaviour == null)
                        continue;

                    // Check for matching sub id
                    if (behaviour.Identity == identity)
                    {
                        // We have matched the data so we can now deserialize
                        behaviourState = state.ReadState(size);

                        // Deserialize the component
                        behaviour.OnReplayDeserialize(behaviourState);

                        // Set matched flags     
                        matchedData = true;
                        break;
                    }
                }

                // Check for found data
                if (matchedData == false)
                {
                    // We need to consume the data to maintain the state
                    for (int i = 0; i < size; i++)
                    {
                        // Call read byte multiple times to save allocating an array
                        state.ReadByte();
                    }
                    Debug.LogWarning("Possible replay state corruption for replay object " + gameObject);
                }
            }
        }

        public bool IsComponentObserved(ReplayBehaviour component)
        {
            foreach (ReplayBehaviour behaviour in ObservedComponents)
                if (component == behaviour)
                    return true;

            return false;
        }
        
        /// <summary>
        /// Forces the object to refresh its list of observed components.
        /// Observed components are components which inherit from <see cref="ReplayBehaviour"/> and exist on either this game object or a child of this game object. 
        /// </summary>
        public void RebuildComponentList()
        {
            // Temp list
            List<ReplayBehaviour> active = new List<ReplayBehaviour>();

            // Process all child behaviour scripts
            foreach (ReplayBehaviour behaviour in GetComponentsInChildren<ReplayBehaviour>(false))
            {
                // Check for deleted components
                if (behaviour == null)
                    continue;

                // Only add the script if it is not marked as ignored
                if (behaviour.GetType().IsDefined(typeof(ReplayIgnoreAttribute), true) == false)
                {
                    // Check for sub object handlers
                    if(behaviour.gameObject != gameObject)
                    {
                        GameObject current = behaviour.gameObject;
                        bool skipBehaviour = false;                        

                        while(true)
                        {
                            if (current.GetComponent<ReplayObject>() != null)
                            {
                                skipBehaviour = true;
                                break;
                            }

                            if (current.transform.parent == null || current.transform.parent == transform)
                                break;

                            // Move up hierarchy
                            current = current.transform.parent.gameObject;
                        }

                        if (skipBehaviour == true)
                            continue;
                    }


                    // Add script
                    active.Add(behaviour);
                }
            }

            // Conver to array
            observedComponents = active.ToArray();

            // Rebuild all parents
            foreach (ReplayObject obj in GetComponentsInParent<ReplayObject>())
                if (obj != this && obj != null)
                    obj.RebuildComponentList();
        }

        public void UpdatePrefabLinks()
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                // Generate the id
                //replayIdentity.Generate();

                // Get the prefab type
                PrefabType type = PrefabUtility.GetPrefabType(gameObject);

                // Disallow scene objects that are active
                if (isActiveAndEnabled == false && type == PrefabType.Prefab)
                {
                    // Store the prefab name
                    prefabIdentity = gameObject.name;
                }
                else
                {
                    // The object is not a prefab
                    prefabIdentity = string.Empty;
                }
            }
#else
            Debug.LogWarning("UpdatePrefabLinks can only be called inside the Unity editor. Calling at runtime will have no effect");
#endif
        }
    }
}
