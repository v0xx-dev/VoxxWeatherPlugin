using UnityEngine;
using VoxxWeatherPlugin.Weathers;
using System.Linq;
using VoxxWeatherPlugin.Utils;

namespace VoxxWeatherPlugin.Behaviours
{
    public class SnowThicknessCalculator : MonoBehaviour
    {
        [SerializeField]
        internal ComputeShader snowThicknessComputeShader;
        [SerializeField]
        internal SnowfallData snowfallData;
        
        [SerializeField]
        internal bool inputNeedsUpdate = true;
        
        internal float[] snowThicknessData = [0.0f];
        [SerializeField]
        internal float snowPositionY = 0.0f;
        internal float lastGroundCollisionPointY = 0.0f;
        internal Vector3 groundNormal = Vector3.up;
        internal Vector3 groundPosition = Vector3.zero;
        internal bool isOnNaturalGround = false;

        private Vector3[] worldSpaceNormalData;
        private int worldSpaceNormalDataSize = 10;
        private int currentWorldSpaceNormalIndex = 0;
        private int kernelHandle;
        private ComputeBuffer worldSpaceNormalBuffer;
        private ComputeBuffer worldSpacePositionBuffer;
        private ComputeBuffer snowThicknessBuffer;

        [Header("Ground")]
        [SerializeField]
        internal GameObject groundObject;
        [SerializeField]
        internal string[] groundTags = {"Grass", "Gravel", "Snow", "Rock"};

        void Start()
        {
            kernelHandle = snowThicknessComputeShader.FindKernel("CSMain");
            snowfallData = SnowfallWeather.Instance.snowfallData;

            // Create buffers for a single normal and position
            worldSpaceNormalBuffer = new ComputeBuffer(1, 3 * sizeof(float));
            worldSpacePositionBuffer = new ComputeBuffer(1, 3 * sizeof(float));
            snowThicknessBuffer = new ComputeBuffer(1, sizeof(float));
            worldSpaceNormalData = new Vector3[worldSpaceNormalDataSize];

            // Set buffers and texture
            snowThicknessComputeShader.SetBuffer(kernelHandle, "_WorldSpaceNormal", worldSpaceNormalBuffer);
            snowThicknessComputeShader.SetBuffer(kernelHandle, "_WorldSpacePosition", worldSpacePositionBuffer);
            snowThicknessComputeShader.SetBuffer(kernelHandle, "_SnowThickness", snowThicknessBuffer);
            snowThicknessComputeShader.SetTexture(kernelHandle, "_DepthTex", snowfallData.levelDepthmap);
            snowThicknessComputeShader.SetTexture(kernelHandle, "_FootprintsTex", snowfallData.snowTracksMap);
        }

        internal void CalculateThickness()
        {

            if (snowfallData == null)
            {
                Debug.LogError("SnowfallData is null, cannot calculate snow thickness!");
                return;
            }

            if (inputNeedsUpdate)
            {
                // Update static input parameters
                snowfallData = SnowfallWeather.Instance.snowfallData;
                // snowThicknessComputeShader.SetFloat("_SnowNoiseScale", snowfallData.snowScale);
                snowThicknessComputeShader.SetFloat("_MaximumSnowHeight", snowfallData.maxSnowHeight);
                snowThicknessComputeShader.SetMatrix("_LightViewProjection", snowfallData.levelDepthmapCamera.projectionMatrix * snowfallData.levelDepthmapCamera.worldToCameraMatrix);
                // snowThicknessComputeShader.SetFloat("_ShadowBias", snowfallData.shadowBias);
                snowThicknessComputeShader.SetFloat("_PCFKernelSize", snowfallData.PCFKernelSize);
                // snowThicknessComputeShader.SetFloat("_SnowOcclusionBias", snowfallData.snowOcclusionBias);
                snowThicknessComputeShader.SetVector("_ShipLocation", snowfallData.shipPosition);
                inputNeedsUpdate = false;
            }

            // Update snow noise power
            snowThicknessComputeShader.SetFloat("_SnowNoisePower", snowfallData.snowIntensity);
            snowThicknessComputeShader.SetMatrix("_FootprintsViewProjection", snowfallData.snowTracksCamera.projectionMatrix * snowfallData.snowTracksCamera.worldToCameraMatrix);
                
            // Update world space normal and position
            (groundNormal, groundPosition) = UpdateWorldSpaceData();
            Debug.LogDebug($"Normal: {groundNormal} Position: {groundPosition}");

            worldSpaceNormalBuffer.SetData(new Vector3[] { groundNormal });
            worldSpacePositionBuffer.SetData(new Vector3[] { groundPosition });
            // Dispatch compute shader
            snowThicknessComputeShader.Dispatch(kernelHandle, 1, 1, 1);

            // Read result
            snowThicknessBuffer.GetData(snowThicknessData);
            snowPositionY = snowThicknessData[0] + lastGroundCollisionPointY;

            Debug.LogDebug($"Snow Thickness: {snowThicknessData[0]}, Actual coordinate: {snowPositionY}");
        }

        internal bool IsGroundTag(Collider collider, string[] tags)
        {
            foreach (string tag in tags)
            {
                if (collider.CompareTag(tag))
                {
                    return true;
                }
            }
            return false;
        }

        internal bool isGroundObject(Collider collider)
        {
            Debug.LogDebug($"{collider.gameObject} == {snowfallData.groundObject} => {collider.gameObject == snowfallData.groundObject}");
            return collider.gameObject == snowfallData.groundObject;
        }

        internal (Vector3, Vector3) UpdateWorldSpaceData()
        {
            // PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            // Vector3 position = localPlayer.controllerCollisionPoint;
            // lastGroundCollisionPointY = position.y;
            // // Store the normal in the buffer
            // worldSpaceNormalData[currentWorldSpaceNormalIndex] = localPlayer.playerGroundNormal;
            // currentWorldSpaceNormalIndex = (currentWorldSpaceNormalIndex + 1) % worldSpaceNormalDataSize;
            // // Moving average of the normals
            // Vector3 averageNormal = worldSpaceNormalData.Aggregate(Vector3.zero, (current, vector) => current + vector);
            // averageNormal /= worldSpaceNormalDataSize;
            // Debug.Log("Normal: " + averageNormal + " Position: " + position);
            // // Set the buffers
            // worldSpaceNormalBuffer.SetData(new Vector3[] { averageNormal });
            // worldSpacePositionBuffer.SetData(new Vector3[] { position });

            Transform playerTransform = GameNetworkManager.Instance.localPlayerController.transform;
            Vector3 normal = Vector3.up;
            Vector3 position = playerTransform.position;
            isOnNaturalGround = false;

            if (snowfallData.groundObject == null)
            {
                Debug.LogError("Ground object is null, cannot calculate snow thickness!");
                return (normal, position);
            }
            
            if (Physics.Raycast(playerTransform.position, -Vector3.up, out RaycastHit hit, 3f,
                                 1 << snowfallData.groundObject.layer, QueryTriggerInteraction.Ignore))
            {
                // if (IsGroundTag(hit.collider, snowfallData.groundTags))
                if (isGroundObject(hit.collider))
                {
                    normal = hit.normal;
                    position = hit.point;
                    isOnNaturalGround = true;
                }
            }
            
            // Store the normal in the buffer
            worldSpaceNormalData[currentWorldSpaceNormalIndex] = normal;
            currentWorldSpaceNormalIndex = (currentWorldSpaceNormalIndex + 1) % worldSpaceNormalDataSize;
            // Moving average of the normals
            Vector3 averageNormal = worldSpaceNormalData.Aggregate(Vector3.zero, (current, vector) => current + vector);
            averageNormal /= worldSpaceNormalDataSize;
            lastGroundCollisionPointY = position.y;

            return (averageNormal, position);
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


