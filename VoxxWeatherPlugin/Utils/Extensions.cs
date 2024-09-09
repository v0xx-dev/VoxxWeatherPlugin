using System;

namespace VoxxWeatherPlugin.Utils
{
    public static class RandomExtensions
    {
        // Extension method for System.Random
        public static float NextDouble(this Random random, float min, float max)
        {
            if (min > max)
            {
                float temp = max;
                max = min;
                min = temp;
                Debug.LogWarning("Minimum value for random range must be less than maximum value. Switching them around!");
            }
            return (float)random.NextDouble() * (max - min) + min;
        }
    }
}
