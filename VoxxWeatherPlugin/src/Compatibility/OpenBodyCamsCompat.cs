using System;
using System.Runtime.CompilerServices;
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

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Init()
        {
            IsActive = true;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
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

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void GlitchBodyCam(MonoBehaviour bodyCamBehaviour)
        {
            BodyCamComponent? bodyCamComp = bodyCamBehaviour as BodyCamComponent;
            GlitchBodyCam(bodyCamComp);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void GlitchBodyCam(BodyCamComponent? bodyCamComp, bool subscribe = true)
        {
            if (bodyCamComp == null)
                return;

            Camera? bodyCam = bodyCamComp.GetCamera();
            GlitchEffect? glitchEffect = SolarFlare?.GlitchCamera(bodyCam);
            RefreshGlitchEffect(bodyCamComp, glitchEffect);
            if (subscribe)
            {
                bodyCamComp.OnTargetChanged += OnCameraStatusChanged;
                bodyCamComp.OnCameraCreated += _ => OnCameraStatusChanged(bodyCamComp);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void OnCameraStatusChanged(MonoBehaviour bodyCamBehaviour)
        {
            if (!SolarFlare?.IsActive ?? true)
                return;

            BodyCamComponent? bodyCamComp = bodyCamBehaviour as BodyCamComponent;
            Camera? bodyCam = bodyCamComp?.GetCamera();
            if (bodyCam == null)
                return;

            if (SolarFlare?.glitchPasses.TryGetValue(bodyCam, out GlitchEffect? glitchEffect) ?? false)
            {
                RefreshGlitchEffect(bodyCamComp, glitchEffect);
            }
            else
            {
                GlitchBodyCam(bodyCamComp, false);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void RefreshGlitchEffect(BodyCamComponent? bodyCamComp, GlitchEffect? glitchEffect)
        {
            if (bodyCamComp == null)
                return;

            if (glitchEffect == null)
            {
                Debug.LogWarning("GlitchEffect is null for bodyCam: " + bodyCamComp.name);
                return;
            }
            
            glitchEffect.enabled = bodyCamComp.IsRemoteCamera && (SolarFlare?.flareData != null);
            glitchEffect.intensity.value = SolarFlare?.flareData?.ScreenDistortionIntensity ?? 0f;
        }
    }
}