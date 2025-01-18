using UnityEngine;
using VoxxWeatherPlugin.Behaviours;
using WeatherRegistry;
namespace VoxxWeatherPlugin.Weathers
{
    public abstract class BaseWeather: MonoBehaviour
    {
        //Define the weather name
        internal abstract string WeatherName { get; }
        public bool IsActive => (gameObject.activeInHierarchy && enabled ||
                                WeatherName.ToLower() == WeatherManager.GetCurrentLevelWeather().Name.ToLower()) &&
                                (!StartOfRound.Instance?.inShipPhase ?? false); // To prevent weather counted as activated in orbit
        protected System.Random? SeededRandom => LevelManipulator.Instance?.seededRandom;
        protected Bounds LevelBounds => LevelManipulator.Instance?.levelBounds ?? default;
        // protected abstract BaseVFXManager VFXManager { get; }

    }

    public abstract class BaseVFXManager: MonoBehaviour
    {
        protected System.Random? SeededRandom => LevelManipulator.Instance?.seededRandom;
        protected Bounds LevelBounds => LevelManipulator.Instance?.levelBounds ?? default;

        internal abstract void Reset();
        internal abstract void PopulateLevelWithVFX();
    }
}