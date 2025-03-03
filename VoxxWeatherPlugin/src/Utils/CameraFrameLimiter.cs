using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace VoxxWeatherPlugin.Utils
{
    internal static class CameraFrameLimiter
    {
        private static readonly Dictionary<Camera, float> cameraRenderTimes = [];

        public static void LimitFrameRate(this Camera camera, float targetFPS)
        {
            if (camera == null)
            {
                Debug.LogError("Cannot limit FPS on a null camera!");
                return;
            }

            if (cameraRenderTimes.TryGetValue(camera, out float lastRenderedFrameTime))
            {
                float frameInterval = targetFPS == 0 ? Mathf.Infinity : 1f / targetFPS;
                frameInterval = targetFPS == -1 ? 0f : frameInterval;
                camera.enabled = Time.time - lastRenderedFrameTime > frameInterval;
                if (camera.enabled)
                {
                    cameraRenderTimes[camera] = Time.time;
                }
                return;
            }
            
            if (cameraRenderTimes.TryAdd(camera, Time.time))
            {
                HDAdditionalCameraData cameraData = camera.GetComponent<HDAdditionalCameraData>();
                cameraData.hasPersistentHistory = true;
            }
        }
    }
}