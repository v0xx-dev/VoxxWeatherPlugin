using UnityEngine;

namespace VoxxWeatherPlugin.Utils
{
    public class LevelManipulator : MonoBehaviour
    {
        public static LevelManipulator Instance { get; private set; }
        public static Bounds levelBounds; // Current level bounds

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(Instance);
            }
            
            Instance = this;
        }

        public static Bounds CalculateLevelSize(float sizeMultiplier = 1.2f)
        {
            levelBounds = new Bounds(Vector3.zero, Vector3.zero);
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

            // Choose the largest dimension of the bounds and make it cube
            float maxDimension = Mathf.Max(levelBounds.size.x, levelBounds.size.z) * sizeMultiplier;
            levelBounds.size = new Vector3(maxDimension, maxDimension, maxDimension);

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