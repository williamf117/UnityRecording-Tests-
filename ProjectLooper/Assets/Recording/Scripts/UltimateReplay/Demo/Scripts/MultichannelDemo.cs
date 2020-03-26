using UnityEngine;
using System.Collections;
using UltimateReplay.Storage;

namespace UltimateReplay.Demo
{
    /// <summary>
    /// A demo script used in the Multichannel demo scene which shows how the multichannel memory target can be used.
    /// </summary>
    public class MultichannelDemo : MonoBehaviour
    {
        // Private
        private ReplayMultichannelMemoryTarget target = null;

        // Public
        /// <summary>
        /// The prefab to spawn.
        /// </summary>
        public GameObject prefab;

        // Methods
        /// <summary>
        /// Called by Unity.
        /// </summary>
        public void Start()
        {
            // Get the replay target
            target = ReplayManager.Target as ReplayMultichannelMemoryTarget;
        }

        /// <summary>
        /// Called by Unity
        /// </summary>
        public void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 280, 180, 100), GUI.skin.box);
            {
                GUILayout.Label("Demo");

                if (GUILayout.Button("Spawn") == true)
                {
                    // Spawn an object
                    Instantiate(prefab, transform.position, Quaternion.identity);
                }

                // Set the enabled flag
                GUI.enabled = ReplayManager.IsRecording == false;

                // Active channel slider
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label(string.Format("Active Channel ({0}):", target.ActiveChannel));
                                        
                    // Display a slider
                    int value = (int)GUILayout.HorizontalSlider(target.ActiveChannel, 0, target.ChannelCount - 1);

                    // Check for change
                    if(value != target.ActiveChannel)
                    {
                        // Make the channel active
                        target.SetActiveChannel(value);
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    if(GUILayout.Button("Add Channel") == true)
                    {
                        // Add a new channel to the target
                        target.AddChannel();
                    }

                    if(GUILayout.Button("Remove Channel") == true)
                    {
                        // Remove a channel
                        target.RemoveChannel();
                    }
                }
                GUILayout.EndHorizontal();

                GUI.enabled = true;
            }
            GUILayout.EndArea();
        }
    }
}
