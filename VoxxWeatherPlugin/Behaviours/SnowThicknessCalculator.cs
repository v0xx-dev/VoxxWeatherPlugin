using UnityEngine;
using VoxxWeatherPlugin.Weathers;

namespace VoxxWeatherPlugin.Behaviours
{
    public class SnowThicknessCalculator : MonoBehaviour
    {
        [SerializeField]
        internal ComputeShader snowThicknessComputeShader;
        [SerializeField]
        internal float distToGround;
        [SerializeField]
        internal SnowfallWeather snowfallWeather;
        
        [SerializeField]
        internal bool inputNeedsUpdate = true;

        private int kernelHandle;
        private ComputeBuffer worldSpaceNormalBuffer;
        private ComputeBuffer worldSpacePositionBuffer;
        private ComputeBuffer snowThicknessBuffer;

        void Start()
        {
            kernelHandle = snowThicknessComputeShader.FindKernel("CSMain");

            // Create buffers for a single normal and position
            worldSpaceNormalBuffer = new ComputeBuffer(1, 3 * sizeof(float));
            worldSpacePositionBuffer = new ComputeBuffer(1, 3 * sizeof(float));
            snowThicknessBuffer = new ComputeBuffer(1, sizeof(float));

            // Set buffers and texture
            snowThicknessComputeShader.SetBuffer(kernelHandle, "_WorldSpaceNormal", worldSpaceNormalBuffer);
            snowThicknessComputeShader.SetBuffer(kernelHandle, "_WorldSpacePosition", worldSpacePositionBuffer);
            snowThicknessComputeShader.SetBuffer(kernelHandle, "_SnowThickness", snowThicknessBuffer);
            snowThicknessComputeShader.SetTexture(kernelHandle, "_DepthTex", snowfallWeather.levelDepthmap);
            snowThicknessComputeShader.SetTexture(kernelHandle, "_FootprintsTex", snowfallWeather.snowTracksMap);

            BoxCollider boxCollider = GameNetworkManager.Instance.localPlayerController.GetComponent<BoxCollider>();

            distToGround = boxCollider.bounds.extents.y;// - boxCollider.center.y;
        }

        void FixedUpdate()
        {
            if (snowfallWeather == null)
            {
                Debug.LogError("SnowfallWeather is null, cannot calculate snow thickness!");
                return;
            }

            if (inputNeedsUpdate)
            {
                // Update static input parameters
                snowThicknessComputeShader.SetFloat("_SnowNoiseScale", snowfallWeather.snowScale);
                snowThicknessComputeShader.SetFloat("_MaximumSnowHeight", snowfallWeather.maxSnowHeight);
                snowThicknessComputeShader.SetMatrix("_LightViewProjection", snowfallWeather.levelDepthmapCamera.projectionMatrix * snowfallWeather.levelDepthmapCamera.worldToCameraMatrix);
                snowThicknessComputeShader.SetMatrix("_FootprintsViewProjection", snowfallWeather.snowTracksCamera.projectionMatrix * snowfallWeather.snowTracksCamera.worldToCameraMatrix);
                snowThicknessComputeShader.SetFloat("_ShadowBias", snowfallWeather.shadowBias);
                snowThicknessComputeShader.SetFloat("_PCFKernelSize", snowfallWeather.PCFKernelSize);
                snowThicknessComputeShader.SetFloat("_SnowOcclusionBias", snowfallWeather.snowOcclusionBias);
                snowThicknessComputeShader.SetVector("_ShipLocation", snowfallWeather.shipPosition);
                inputNeedsUpdate = false;
            }

            // Update snow noise power
            snowThicknessComputeShader.SetFloat("_SnowNoisePower", snowfallWeather.snowIntensity);

            // Update world space normal and position
            UpdateWorldSpaceData();

            // Dispatch compute shader
            snowThicknessComputeShader.Dispatch(kernelHandle, 1, 1, 1);

            // Read result
            float[] snowThicknessData = new float[1];
            snowThicknessBuffer.GetData(snowThicknessData);

            Debug.Log("Snow Thickness: " + snowThicknessData[0]);
        }

        void UpdateWorldSpaceData()
        {
            Transform playerTransform = GameNetworkManager.Instance.localPlayerController.transform;
            Vector3 normal = Vector3.up;
            Vector3 position = playerTransform.position;

            if (Physics.Raycast(playerTransform.position, -playerTransform.up, out RaycastHit hit, distToGround, LayerMask.GetMask("Room")))
            {
                if (hit.collider.CompareTag("Grass"))
                {
                    normal = hit.normal;
                    position = hit.point;
                    Debug.Log("Hit Grass");
                }
            }
            Debug.Log($"Normal: {normal} Position: {position}");

            worldSpaceNormalBuffer.SetData(new Vector3[] { normal });
            worldSpacePositionBuffer.SetData(new Vector3[] { position });
        }

        void OnDestroy()
        {
            // Clean up
            worldSpaceNormalBuffer?.Release();
            worldSpacePositionBuffer?.Release();
            snowThicknessBuffer?.Release();
        }
    }
}


