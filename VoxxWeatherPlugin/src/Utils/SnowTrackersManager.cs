using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using GameNetcodeStuff;
using VoxxWeatherPlugin.Behaviours;
using System.Collections;
using VoxxWeatherPlugin.Patches;

namespace VoxxWeatherPlugin.Utils
{
    public enum TrackerType
    {
        Footprints, // Leave trailing footprints
        FootprintsLowCapacity, // Leave trailing footprints, but have a lower particle capacity
        Shovel, // Shoots a burst of snow removing particles forward (must be played manually)
        Item // Leaves a single footprint,for example when dropping an item (must be played manually)
    }

    public class SnowTrackerData
    {
        public VisualEffect? trackerVFX;
        public bool isTracking;

        public SnowTrackerData(VisualEffect trackerVFX, bool isTracking = false)
        {
            this.trackerVFX = trackerVFX;
            this.isTracking = isTracking;
        }
    }

    //TODO CHECK IF IT'S WORKING
    public static class SnowTrackersManager
    {
        internal static GameObject? snowTrackersContainer;
        public static Dictionary<MonoBehaviour, SnowTrackerData> snowTrackersDict = [];
        public static Dictionary<MonoBehaviour, SnowTrackerData> snowShovelDict = [];
        private static readonly int isTrackingID = Shader.PropertyToID("isTracking");

        /// <summary>
        /// Registers a snow footprint tracker for the specified object.
        /// </summary>
        /// <param name="obj">The object to track.</param>
        /// <param name="trackerVariant">The type of tracker to use.</param>
        /// <param name="particleSize">The size of the particles in the tracker.</param>
        /// <param name="lifetimeMultiplier">The lifetime multiplier of the particles in the tracker.</param>
        /// <param name="footprintStrength">The strength of the footprints, i.e. how deep they are</param>
        /// <param name="positionOffset">The offset to apply to the tracker's position.</param>
        public static void RegisterFootprintTracker(MonoBehaviour obj,
                                                    TrackerType trackerVariant,
                                                    float particleSize = 1f,
                                                    float lifetimeMultiplier = 1f,
                                                    float footprintStrength = 1f,
                                                    Vector3 positionOffset = default)
        {
            if (snowTrackersContainer == null)
            {
                // Must be in SampleSceneRelay otherwise VFX causes a crash for some reason
                snowTrackersContainer = new GameObject("SnowTrackersContainer");
                snowTrackersContainer.SetActive(false);
                GameObject.DontDestroyOnLoad(snowTrackersContainer);
            }   

            VisualEffectAsset? trackerVariantVFX = trackerVariant switch
            {
                TrackerType.Footprints => LevelManipulator.snowTrackersDict?["footprintsTrackerVFX"],
                TrackerType.FootprintsLowCapacity => LevelManipulator.snowTrackersDict?["lowcapFootprintsTrackerVFX"],
                TrackerType.Shovel => LevelManipulator.snowTrackersDict?["shovelVFX"],
                TrackerType.Item => LevelManipulator.snowTrackersDict?["itemTrackerVFX"],
                _ => null
            };

            GameObject trackerObj;
            trackerObj = new GameObject("FootprintsTracker_" + trackerVariant + "_" + obj.name);
            if (trackerVariant is TrackerType.Shovel || trackerVariant is TrackerType.Item)
            {
                // GrabbableObject has no OnDestroy method, where we could catch the tracker, so we never parent to it to avoid particles disappearing
                trackerObj.transform.SetParent(snowTrackersContainer.transform);
            }
            else
            {
                trackerObj.transform.SetParent(obj.transform);
            }
            trackerObj.transform.localPosition = Vector3.zero + positionOffset;
            trackerObj.transform.localScale = Vector3.one;
            trackerObj.layer = LayerMask.NameToLayer("Vehicle"); // Must match the culling mask of the FootprintsTrackerCamera in SnowfallWeather
            if (!SnowPatches.IsSnowActive())
            {
                trackerObj.SetActive(false);
            }
            VisualEffect trackerVFX = trackerObj.AddComponent<VisualEffect>();
            trackerVFX.visualEffectAsset = trackerVariantVFX;

            SnowTrackerData snowTrackerData = new(trackerVFX);

            if (trackerVariant is TrackerType.Shovel)
            {
                //rotate around local Y axis by 90 degrees to align with the player's camera
                trackerObj.transform.localRotation = Quaternion.Euler(0, 90, 0);
                snowShovelDict.TryAdd(obj, snowTrackerData);
            }
            else
            {
                trackerObj.transform.localRotation = Quaternion.identity;
                trackerVFX.SetFloat("particleSize", particleSize);
                trackerVFX.SetFloat("lifetimeMultiplier", lifetimeMultiplier);
                trackerVFX.SetFloat("footprintStrength", footprintStrength);
                snowTrackersDict.TryAdd(obj, snowTrackerData);
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
        public static void UpdateFootprintTracker(MonoBehaviour obj, bool enableTracker)
        {
            if (!Configuration.enableSnowTracks.Value)
                return;

            if (snowTrackersDict.TryGetValue(obj, out SnowTrackerData trackerData))
            {
                VisualEffect? footprintsTrackerVFX = trackerData.trackerVFX;
                bool trackingNeedsUpdating = trackerData.isTracking ^ enableTracker;
                if (trackingNeedsUpdating)
                {
                    footprintsTrackerVFX?.SetBool(isTrackingID, enableTracker);
                    trackerData.isTracking = enableTracker;
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
            Dictionary<MonoBehaviour, SnowTrackerData> dictForVFX = trackerVariant is TrackerType.Shovel ? snowShovelDict : snowTrackersDict;

            if (dictForVFX.TryGetValue(obj, out SnowTrackerData trackerData) && playCondition)
            {
                VisualEffect? footprintsTrackerVFX = trackerData.trackerVFX;
                if (footprintsTrackerVFX == null)
                {
                    return;
                }
                // Set the position of the tracker to the object's position
                footprintsTrackerVFX.transform.position = obj.transform.position; 
                footprintsTrackerVFX.Play();
            }
        }
        
        internal static void AddFootprintTracker(MonoBehaviour obj,
                                                float particleSize,
                                                float lifetimeMultiplier,
                                                float footprintStrength,
                                                Vector3 positionOffset = default)
        {
            if (!Configuration.enableSnowTracks.Value)
                return;

            //Load different footprints for player and other objects
            switch (obj)
            {
                case PlayerControllerB:
                    RegisterFootprintTracker(obj, TrackerType.Footprints, particleSize, lifetimeMultiplier, footprintStrength, positionOffset);
                    break;
                case EnemyAI:
                    RegisterFootprintTracker(obj, TrackerType.FootprintsLowCapacity, particleSize, lifetimeMultiplier, footprintStrength, positionOffset);
                    break;
                case GrabbableObject:
                    RegisterFootprintTracker(obj, TrackerType.Item, particleSize, lifetimeMultiplier, footprintStrength, positionOffset);
                    break;
                case VehicleController:
                    RegisterFootprintTracker(obj, TrackerType.Footprints, particleSize, lifetimeMultiplier, footprintStrength, positionOffset);
                    break;
            }
                
            if (obj is Shovel)
            {
                RegisterFootprintTracker(obj, TrackerType.Shovel, 2f);
            }
        }

        /// <summary>
        /// Temporarily saves the tracker to the snowTrackersContainer to prevent vanishing of the particles when the main object is destroyed
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="trackerVariant"></param>
        internal static void TempSaveTracker(MonoBehaviour obj, TrackerType trackerVariant)
        {
            Dictionary<MonoBehaviour, SnowTrackerData> dictForVFX = trackerVariant is TrackerType.Shovel ? snowShovelDict : snowTrackersDict;

            if (dictForVFX.TryGetValue(obj, out SnowTrackerData trackerData) && trackerData.trackerVFX != null)
            {
                VisualEffect footprintsTrackerVFX = trackerData.trackerVFX;
                // Move the tracker to the container temporarily if the main object is destroyed to prevent vanishing of the particles
                footprintsTrackerVFX.transform.SetParent(snowTrackersContainer?.transform); 
            }
        }

        /// <summary>
        /// Restores the tracker to the object from the snowTrackersContainer
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="trackerVariant"></param>
        internal static void RestoreSavedTracker(MonoBehaviour obj, TrackerType trackerVariant)
        {
            Dictionary<MonoBehaviour, SnowTrackerData> dictForVFX = trackerVariant is TrackerType.Shovel ? snowShovelDict : snowTrackersDict;

            if (dictForVFX.TryGetValue(obj, out SnowTrackerData trackerData) && trackerData.trackerVFX != null)
            {
                VisualEffect footprintsTrackerVFX = trackerData.trackerVFX;
                // Restore the tracker to the object
                footprintsTrackerVFX.transform.SetParent(obj?.transform); 
            }
        }

        // Removes stale entries from the dictionary and toggles the trackers on or off
        private static void CleanupFootprintTrackers(Dictionary<MonoBehaviour, SnowTrackerData> trackersDict, bool enable = false)
        {
            List<MonoBehaviour> keysToRemove = []; // Store keys to remove
            
            if (snowTrackersContainer != null)
                snowTrackersContainer.SetActive(enable);

            foreach ((var obj, var trackerData) in trackersDict) 
            {
                if (obj == null) // Check if the object has been destroyed
                {
                    if (trackerData.trackerVFX != null)
                        GameObject.Destroy(trackerData.trackerVFX.gameObject);
                    keysToRemove.Add(obj);
                }
                else
                {
                    trackerData.trackerVFX?.gameObject.SetActive(enable);
                }
            }

            Debug.LogDebug($"Removing {keysToRemove.Count} previously destroyed entries from snow footprint trackers");

            foreach (var key in keysToRemove)
            {
                trackersDict.Remove(key);
            }
        }

        internal static void CleanupTrackers(bool enable = false)
        {
            if (!Configuration.enableSnowTracks.Value)
                return;

            if (snowTrackersDict.Count > 0 || snowShovelDict.Count > 0)
            {
                Debug.LogDebug("Cleaning up snow footprint trackers");
                CleanupFootprintTrackers(snowTrackersDict, enable);
                CleanupFootprintTrackers(snowShovelDict, enable);
            }
        }
    }
}