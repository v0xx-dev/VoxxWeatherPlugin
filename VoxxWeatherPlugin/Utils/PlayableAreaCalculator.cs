using System.Collections.Generic;
using UnityEngine;

namespace VoxxWeatherPlugin.Utils
{
    public class PlayableAreaCalculator
    {
       public static (Vector3, Vector3) CalculateZoneSize()
        {
            List<Vector3> keyLocationCoords = [StartOfRound.Instance.shipInnerRoomBounds.bounds.center];

            // Store positions of all the outside AI nodes in the scene
            foreach (GameObject node in RoundManager.Instance.outsideAINodes)
            {
                if (node == null)
                    continue;
                keyLocationCoords.Add(node.transform.position);
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
                    Vector3 entrancePointCoords = entranceTeleport.entrancePoint.position;
                    keyLocationCoords.Add(entrancePointCoords);
                }
            }

            // Calculate the size of the heatwave zone based on the key locations
            Vector3 minCoords = keyLocationCoords[0];
            Vector3 maxCoords = keyLocationCoords[0];

            foreach (Vector3 coords in keyLocationCoords)
            {
                minCoords = Vector3.Min(minCoords, coords);
                maxCoords = Vector3.Max(maxCoords, coords);
            }

            Vector3 zoneSize = (maxCoords - minCoords)*1.2f; //Inflate the zone size by 20%
            Vector3 zoneCenter = (minCoords + maxCoords) / 2f;

            return (zoneSize, zoneCenter);

        } 
    }
}