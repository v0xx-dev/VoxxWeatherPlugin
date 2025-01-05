using UnityEngine;

namespace VoxxWeatherPlugin.Weathers
{
    public abstract class BaseWeather: MonoBehaviour
    {
        //Define the weather name
        // TODO change is active to comparing the name to the one from WeatherManager
        public bool IsActive => gameObject.activeInHierarchy && enabled;
        // public BaseVFXManager VFXManager;
    }

    public abstract class BaseVFXManager: MonoBehaviour
    {
        internal abstract void Reset();
        internal abstract void PopulateLevelWithVFX(System.Random? seededRandom = null);
    }
}