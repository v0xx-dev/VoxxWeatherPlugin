using UnityEngine;

namespace VoxxWeatherPlugin.Weathers
{
    public abstract class BaseWeather: MonoBehaviour
    {
        public bool IsActive => gameObject.activeInHierarchy && enabled;
        // public BaseVFXManager VFXManager;
    }

    public abstract class BaseVFXManager: MonoBehaviour
    {
    }
}