using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin.Utils
{
    public enum TrackerType
    {
        Footprints, // Leave trailing footprints
        FootprintsLowCapacity, // Leave trailing footprints, but have a lower particle capacity
        Shovel, // Shoots a burst of snow removing particles forward (must be played manually)
        Item // Leaves a single footprint,for example when dropping an item (must be played manually)
    }

    public static class SnowTrackersManager
    {
        internal static GameObject? snowTrackersContainer;
        public static Dictionary<MonoBehaviour, VisualEffect> snowTrackersDict = new Dictionary<MonoBehaviour, VisualEffect>();
        public static Dictionary<MonoBehaviour, VisualEffect> snowShovelDict = new Dictionary<MonoBehaviour, VisualEffect>();
        private static readonly int isTrackingID = Shader.PropertyToID("isTracking");

        /// <summary>
        /// Registers a snow footprint tracker for the specified object.
        /// </summary>
        /// <param name="obj">The object to track.</param>
        /// <param name="trackerVariant">The type of tracker to use.</param>
        /// <param name="particleSize">The size of the particles in the tracker.</param>
        /// <param name="lifetimeMultiplier">The lifetime multiplier of the particles in the tracker.</param>
        /// <param name="footprintStrength">The strength of the footprints, i.e. how deep they are</param>
        public static void RegisterFootprintTracker(MonoBehaviour obj, TrackerType trackerVariant, float particleSize = 1f, float lifetimeMultiplier = 1f, float footprintStrength = 1f)
        {
            if (snowTrackersContainer == null)
            {
                // Must be in SampleSceneRelay otherwise VFX causes a crash for some reason
                snowTrackersContainer = new GameObject("SnowTrackersContainer");
                GameObject.DontDestroyOnLoad(snowTrackersContainer);
            }   

            VisualEffectAsset? trackerVariantVFX = trackerVariant switch
            {
                TrackerType.Footprints => SnowfallVFXManager.snowTrackersDict?["footprintsTrackerVFX"],
                TrackerType.FootprintsLowCapacity => SnowfallVFXManager.snowTrackersDict?["lowcapFootprintsTrackerVFX"],
                TrackerType.Shovel => SnowfallVFXManager.snowTrackersDict?["shovelVFX"],
                TrackerType.Item => SnowfallVFXManager.snowTrackersDict?["itemTrackerVFX"],
                _ => null
            };

            GameObject trackerObj;
            trackerObj = new GameObject("FootprintsTracker_" + trackerVariant + "_" + obj.name); 
            trackerObj.transform.SetParent(snowTrackersContainer?.transform);
            trackerObj.transform.localPosition = Vector3.zero;
            trackerObj.transform.localScale = Vector3.one;
            trackerObj.layer = LayerMask.NameToLayer("Vehicle"); // Must match the culling mask of the FootprintsTrackerCamera in SnowfallWeather
        
            VisualEffect trackerVFX = trackerObj.AddComponent<VisualEffect>();
            trackerVFX.visualEffectAsset = trackerVariantVFX;

            if (trackerVariant is TrackerType.Shovel)
            {
                //rotate around local Y axis by 90 degrees to align with the player's camera
                trackerObj.transform.localRotation = Quaternion.Euler(0, 90, 0);
                snowShovelDict.Add(obj, trackerVFX);
            }
            else
            {
                trackerObj.transform.localRotation = Quaternion.identity;
                trackerVFX.SetFloat("particleSize", particleSize);
                trackerVFX.SetFloat("lifetimeMultiplier", lifetimeMultiplier);
                trackerVFX.SetFloat("footprintStrength", footprintStrength);
                snowTrackersDict.Add(obj, trackerVFX);
            }
        }

        /// <summary>
        /// Updates the position and tracking state of a snow footprint tracker.
        /// </summary>
        /// <param name="obj">The object to track.</param>
        /// <param name="enableTracker">Whether to enable the tracker.</param>
        /// <param name="offset">The offset to apply to the tracker's position.</param>
        /// <remarks>
        /// This method should be called every frame to keep the tracker up to date.
        /// </remarks>
        public static void UpdateFootprintTracker(MonoBehaviour obj, bool enableTracker, Vector3 offset = default)
        {
            if (snowTrackersDict.TryGetValue(obj, out VisualEffect footprintsTrackerVFX))
            {
                footprintsTrackerVFX.transform.position = obj.transform.position + offset;
                bool trackingNeedsUpdating = footprintsTrackerVFX.GetBool(isTrackingID) ^ enableTracker;
                if (trackingNeedsUpdating)
                {
                    footprintsTrackerVFX.SetBool(isTrackingID, enableTracker);
                }
            }
        }

        /// <summary>
        /// Plays a snow footprint tracker.
        /// </summary>
        /// <param name="obj">The object to track.</param>
        /// <param name="trackerVariant">The type of tracker to play.</param>
        /// <param name="playCondition">Whether to play the tracker.</param>
        public static void PlayFootprintTracker(MonoBehaviour obj, TrackerType trackerVariant, bool playCondition = false)
        {
            Dictionary<MonoBehaviour, VisualEffect> dictForVFX;
            if (trackerVariant is TrackerType.Shovel)
            {
                dictForVFX = snowShovelDict;
            }
            else
            {
                dictForVFX = snowTrackersDict;
            }

            if (dictForVFX.TryGetValue(obj, out VisualEffect footprintsTrackerVFX) && playCondition)
            {
                footprintsTrackerVFX.transform.position = obj.transform.position;
                footprintsTrackerVFX?.Play();
            }
        }
        
        internal static void AddFootprintTracker(MonoBehaviour obj, float particleSize, float lifetimeMultiplier, float footprintStrength)
        {
            //Load different footprints for player and other objects
            switch (obj)
            {
                case PlayerControllerB:
                    RegisterFootprintTracker(obj, TrackerType.Footprints, particleSize, lifetimeMultiplier, footprintStrength);
                    break;
                case EnemyAI:
                    RegisterFootprintTracker(obj, TrackerType.FootprintsLowCapacity, particleSize, lifetimeMultiplier, footprintStrength);
                    break;
                case GrabbableObject:
                    RegisterFootprintTracker(obj, TrackerType.Item, particleSize, lifetimeMultiplier, footprintStrength);
                    break;
                case VehicleController:
                    RegisterFootprintTracker(obj, TrackerType.Footprints, particleSize, lifetimeMultiplier, footprintStrength);
                    break;
            }

            if (obj is Shovel)
            {
                RegisterFootprintTracker(obj, TrackerType.Shovel);
            }
        }

        // Removes stale entries from the dictionary
        internal static void CleanupFootprintTrackers(Dictionary<MonoBehaviour, VisualEffect> trackersDict)
        {
            Debug.LogDebug("Cleaning up snow footprint trackers");

            List<MonoBehaviour> keysToRemove = new List<MonoBehaviour>(); // Store keys to remove

            foreach (var keyValuePair in trackersDict) 
            {
                if (keyValuePair.Key == null) // Check if the object has been destroyed
                {
                    if (keyValuePair.Value != null)
                        GameObject.Destroy(keyValuePair.Value.gameObject);
                    keysToRemove.Add(keyValuePair.Key);
                }
            }

            Debug.LogDebug($"Removing {keysToRemove.Count} previously destroyed entries from snow footprint trackers");

            foreach (var key in keysToRemove)
            {
                trackersDict.Remove(key);
            }
        }

        internal static void ToggleFootprintTrackers(bool enable)
        {
            snowTrackersContainer?.SetActive(enable);
        }
    }
}