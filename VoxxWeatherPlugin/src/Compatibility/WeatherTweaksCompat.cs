using System.Runtime.CompilerServices;
using VoxxWeatherPlugin.Utils;
using WeatherRegistry;
using WeatherTweaks.Definitions;

namespace VoxxWeatherPlugin.Compatibility
{
    internal static class WeatherTweaksCompat
    {
        public static bool IsActive { get; private set; } = false;
        public static bool IsWeatherRegistered { get; private set; } = false;

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Init()
        {
            IsActive = true;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void RegisterCombinedWeathers()
        {
            if (IsWeatherRegistered)
            {
                return;
            }

            if (Configuration.EnableSolarFlareWeather.Value)
            {
                new CombinedWeatherType("Eclipsed Flare",
                                        [new WeatherNameResolvable("solarflare"), new WeatherTypeResolvable(LevelWeatherType.Eclipsed)]
                );

                if (Configuration.EnableSnowfallWeather.Value)
                {
                    new CombinedWeatherType("Snowfall + Solar Flare",
                                        [new WeatherNameResolvable("snowfall"), new WeatherNameResolvable("solarflare")]
                    );
                }

                if (Configuration.EnableHeatwaveWeather.Value)
                { 
                    new ProgressingWeatherType("Solar Flare > Heatwave",
                                                  new WeatherNameResolvable("solarflare"),
                                                  [
                                                    new ProgressingWeatherEntry
                                                    {
                                                      DayTime = 0.6f,
                                                      Chance = 1.0f,
                                                      Weather = new WeatherNameResolvable("heatwave")
                                                    }
                                                  ]
                    );
                }
            }

            if (Configuration.EnableSnowfallWeather.Value)
            {
                new ProgressingWeatherType("Snowfall > Rainy",
                                                  new WeatherNameResolvable("snowfall"),
                                                  [
                                                    new ProgressingWeatherEntry
                                                    {
                                                      DayTime = 0.5f,
                                                      Chance = 0.75f,
                                                      Weather = new WeatherTypeResolvable(LevelWeatherType.Rainy)
                                                    },

                                                    new ProgressingWeatherEntry
                                                    {
                                                      DayTime = 0.75f,
                                                      Chance = 1f,
                                                      Weather = new WeatherTypeResolvable(LevelWeatherType.Rainy)
                                                    }
                                                  ]
                );
            }

            IsWeatherRegistered = true;
            Debug.LogDebug("Registered custom combined and progressing weathers!");
        }
    }
}
