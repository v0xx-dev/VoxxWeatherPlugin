using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace VoxxWeatherPlugin.Utils
{
    public class PlayableAreaCalculator
    {
       public static Bounds CalculateZoneSize()
        {
            Bounds levelBounds = new Bounds(Vector3.zero, Vector3.zero);
            levelBounds.Encapsulate(StartOfRound.Instance.shipInnerRoomBounds.bounds);

            // Store positions of all the outside AI nodes in the scene
            foreach (GameObject node in RoundManager.Instance.outsideAINodes)
            {
                if (node == null)
                    continue;
                levelBounds.Encapsulate(node.transform.position);
            }

            // Find all Entrances in the scene
            EntranceTeleport[] entranceTeleports = GameObject.FindObjectsOfType<EntranceTeleport>();

            foreach (EntranceTeleport entranceTeleport in entranceTeleports)
            {
                if (entranceTeleport == null)
                    continue;
                // Check if the entrance is on the outside
                if (entranceTeleport.isEntranceToBuilding)
                {
                    levelBounds.Encapsulate(entranceTeleport.entrancePoint.position);
                }
            }

            //Increase the zone extents by 20%
            levelBounds.extents *= 1.2f;

            Debug.LogDebug("Level bounds: " + levelBounds);
            
#if DEBUG
            GameObject debugCube = new GameObject("LevelBounds");
            BoxCollider box = debugCube.AddComponent<BoxCollider>();
            box.transform.position = levelBounds.center;
            box.size = levelBounds.size;
            box.isTrigger = true;
            GameObject.Instantiate(debugCube);
#endif

            return levelBounds;

        } 
    }
}