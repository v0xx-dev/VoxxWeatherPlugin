using System;

namespace VoxxWeatherPlugin.Utils
{
    public static class RandomExtensions
    {
        // Extension method for System.Random
        public static float NextDouble(this Random random, float min, float max)
        {
            if (min >= max)
            {
                throw new ArgumentException("Minimum value must be less than maximum value.");
            }
            return (float)random.NextDouble() * (max - min) + min;
        }
    }
}
