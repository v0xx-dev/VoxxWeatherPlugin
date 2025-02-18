using System;
using OpenBodyCams;
using OpenBodyCams.API;
using UnityEngine;
using VoxxWeatherPlugin.Behaviours;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin.Compatibility
{
    public static class OpenBodyCamsCompat
    {
        public static bool IsActive { get; private set; } = false;
        private static SolarFlareWeather? SolarFlare => SolarFlareWeather.Instance;

        public static void Init()
        {
            IsActive = true;
        }

        internal static void GlitchBodyCameras()
        {
            BodyCamComponent[] bodyCamComps = BodyCamComponent.GetAllBodyCams();

            foreach (BodyCamComponent bodyCamComp in bodyCamComps)
            {
                GlitchBodyCam(bodyCamComp);
            }

            //Subscribe to know about cameras instantiated after this point
            BodyCam.OnBodyCamInstantiated += GlitchBodyCam;
        }

        private static void GlitchBodyCam(MonoBehaviour bodyCamBehaviour)
        {
            BodyCamComponent? bodyCamComp = bodyCamBehaviour as BodyCamComponent;
            GlitchBodyCam(bodyCamComp);
        }

        internal static void GlitchBodyCam(BodyCamComponent? bodyCamComp)
        {
            if (bodyCamComp == null)
                return;

            Camera? bodyCam = bodyCamComp.GetCamera();
            GlitchEffect? glitchEffect = SolarFlare?.GlitchCamera(bodyCam);
            RefreshGlitchEffect(bodyCamComp, glitchEffect);
            bodyCamComp.OnTargetChanged += OnCameraStatusChanged;
        }

        private static void OnCameraStatusChanged(MonoBehaviour bodyCamBehaviour)
        {
            if (!SolarFlare?.IsActive ?? true)
                return;

            BodyCamComponent? bodyCamComp = bodyCamBehaviour as BodyCamComponent;
            if (SolarFlare?.glitchPasses.TryGetValue(bodyCamComp?.GetCamera(), out GlitchEffect? glitchEffect) ?? false)
            {
                RefreshGlitchEffect(bodyCamComp, glitchEffect);
            }
        }

        private static void RefreshGlitchEffect(BodyCamComponent? bodyCamComp, GlitchEffect? glitchEffect)
        {
            if (bodyCamComp == null)
                return;

            if (glitchEffect == null)
            {
                Debug.LogWarning("GlitchEffect is null for bodyCam: " + bodyCamComp.name);
                return;
            }

            glitchEffect.enabled = bodyCamComp.IsRemoteCamera && (SolarFlare?.IsActive ?? false);
            glitchEffect.intensity.value = SolarFlare?.flareData?.ScreenDistortionIntensity ?? 0.1f;

        }
    }
}