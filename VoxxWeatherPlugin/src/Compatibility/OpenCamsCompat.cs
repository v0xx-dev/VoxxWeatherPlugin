namespace VoxxWeatherPlugin.Compatibility
{
    public static class OpenCamsCompat
    {
        ///  private setter
        public static bool isActive { get; private set; } = false;

        public static void Init()
        {
            isActive = true;
        }
    }
}