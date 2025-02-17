using Unity.Netcode;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin.Behaviours
{
    public class WeatherEventSynchronizer: NetworkBehaviour
    {
        public static WeatherEventSynchronizer Instance { get; private set; } = null!;

        internal void Awake()
        {
            Instance = this;
        }

        internal void StartMalfunction(ElectricMalfunctionData malfunctionData)
        {
            if (IsServer)
            {
                if (malfunctionData.malfunctionObject is EnemyAINestSpawnObject radMechNest)
                {
                    ResolveMalfunctionClientRpc(RoundManager.Instance.enemyNestSpawnObjects.IndexOf(radMechNest));
                }
                else if (malfunctionData.malfunctionObject is NetworkBehaviour malfunctionObject)
                {
                    NetworkBehaviourReference malfunctionDataRef = new NetworkBehaviourReference(malfunctionObject);
                    ResolveMalfunctionClientRpc(malfunctionDataRef);
                }
            }
        }

        [ClientRpc]
        internal void ResolveMalfunctionClientRpc(NetworkBehaviourReference malfunctionObjectRef)
        {
            if (malfunctionObjectRef.TryGet(out NetworkBehaviour malfunctionObject))
            {
                if (SolarFlareWeather.Instance?.electricMalfunctionData?.TryGetValue(malfunctionObject, out ElectricMalfunctionData malfunctionData) ?? false)
                {
                    StartCoroutine(SolarFlareWeather.Instance?.ElectricMalfunctionCoroutine(malfunctionData));
                }
            }
        }

        [ClientRpc]
        internal void ResolveMalfunctionClientRpc(int radMechNestIndex)
        {
            EnemyAINestSpawnObject radMechNest = RoundManager.Instance.enemyNestSpawnObjects[radMechNestIndex];
            if (SolarFlareWeather.Instance?.electricMalfunctionData?.TryGetValue(radMechNest, out ElectricMalfunctionData malfunctionData) ?? false)
            {
                StartCoroutine(SolarFlareWeather.Instance?.ElectricMalfunctionCoroutine(malfunctionData));
            }
        }

    }
}