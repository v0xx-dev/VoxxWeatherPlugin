using System;
using UnityEngine;

namespace VoxxWeatherPlugin.Utils
{
    [Serializable] 
    public class SnowfallData
    {
        [Header("Visuals")]
        public Material iceMaterial;
        public float emissionMultiplier;

        [Header("Base Snow Thickness")]
        public float snowScale = 1.0f;
        [SerializeField, Tooltip("The snow intensity is the power of the exponential function, so 0.0f is full snow.")]
        public float snowIntensity = 10.0f;
        public float maxSnowHeight = 2.0f;
        public float maxSnowNormalizedTime = 1f;

        [Header("Snow Occlusion")]
        public Camera levelDepthmapCamera;
        public RenderTexture levelDepthmap;
        public uint PCFKernelSize = 9;
        public float shadowBias = 0.01f;
        public float snowOcclusionBias = 0.01f;

        [Header("Snow Tracks")]
        public GameObject snowTrackerCameraContainer;
        public Camera snowTracksCamera;
        public RenderTexture snowTracksMap;
        public GameObject footprintsTrackerVFX;
        public GameObject lowcapFootprintsTrackerVFX;
        public GameObject itemTrackerVFX;
        public GameObject shovelVFX;

        [Header("Tessellation Parameters")]
        public float baseTessellationFactor = 4.0f;
        public float maxTessellationFactor = 16.0f;
        public int isAdaptiveTessellation = 0; // 0 for fixed tessellation, 1 for adaptive tessellation

        [Header("Baking Parameters")]
        public Material bakeMaterial;
        public int bakeResolution = 1024;
        public float blurRadius = 3.0f;

        [Header("Terrain Mesh Post-Processing")]
        public float baseEdgeLength = 5;
        public bool useBounds = true;
        public bool subdivideMesh= true;
        public bool smoothMesh = true;
        public int smoothingIterations = 1;
        public bool replaceUvs = false;
        public bool constrainEdges = true;

        [Header("TerraMesh Parameters")]
        public Shader terraMeshShader;
        public bool refineMesh = false;
        public bool carveHoles = false;
        public bool copyTrees = false;
        public bool copyDetail = false;
        public bool useMeshCollider = false;
        
        public int targetVertexCount = -1;
        public int minMeshStep = 1;
        public int maxMeshStep = 16;
        public float falloffSpeed = 2f;

        [Header("General")]
        public Vector3 shipPosition;
        public Shader snowVertexShader;

        internal void RefreshBakeMaterial()
        {
            // Set shader properties
            bakeMaterial.SetFloat("_SnowNoiseScale", snowScale);
            bakeMaterial.SetFloat("_ShadowBias", shadowBias);
            bakeMaterial.SetFloat("_PCFKernelSize", PCFKernelSize);
            bakeMaterial.SetFloat("_BlurKernelSize", blurRadius);
            bakeMaterial.SetFloat("_SnowOcclusionBias", snowOcclusionBias);
            bakeMaterial.SetVector("_ShipLocation", shipPosition);
            
            if (levelDepthmap != null)
            {
                bakeMaterial.SetTexture("DepthTex", levelDepthmap);
            }

            // Set projection matrix from camera
            if (levelDepthmapCamera != null)
            {
                Matrix4x4 viewMatrix = levelDepthmapCamera.worldToCameraMatrix;
                Matrix4x4 projMatrix = levelDepthmapCamera.projectionMatrix;
                bakeMaterial.SetMatrix("_LightViewProjection", projMatrix * viewMatrix);
            }
        }

        internal void RefreshSnowMaterial(Material snowMaterial)
        {
            snowMaterial.SetFloat("_SnowNoiseScale", snowScale);
            snowMaterial.SetFloat("_SnowIntensity", snowIntensity);
            snowMaterial.SetFloat("_MaxSnowHeight", maxSnowHeight);
            snowMaterial.SetFloat("_MaxSnowNormalizedTime", maxSnowNormalizedTime);
        }
    }


    public enum FlareIntensity
    {
        Weak,
        Mild,
        Average,
        Strong
    }

    [Serializable]
    public class FlareData
    {
        public FlareIntensity Intensity;
        public float ScreenDistortionIntensity;
        public float RadioDistortionIntensity;
        public float RadioBreakthroughLength;
        public float RadioFrequencyShift;
        public float FlareSize;
        public Color AuroraColor1;
        public Color AuroraColor2;
        public bool IsDoorMalfunction;

        public FlareData(FlareIntensity intensity = FlareIntensity.Average)
        {
            Intensity = intensity;

            switch (intensity)
            {
                case FlareIntensity.Weak:
                    ScreenDistortionIntensity = 0.3f;
                    RadioDistortionIntensity = 0.25f;
                    RadioBreakthroughLength = 1.25f;
                    RadioFrequencyShift = 1000f;
                    AuroraColor1 = new Color(0f, 11.98f, 0.69f, 1f); 
                    AuroraColor2 = new Color(0.29f, 8.33f, 8.17f, 1f);
                    FlareSize = 1f;
                    IsDoorMalfunction = false;
                    break;
                case FlareIntensity.Mild:
                    ScreenDistortionIntensity = 0.5f;
                    RadioDistortionIntensity = 0.45f;
                    RadioBreakthroughLength = 0.75f;
                    RadioFrequencyShift = 250f;
                    AuroraColor1 = new Color(0.13f, 8.47f, 8.47f, 1f);
                    AuroraColor2 = new Color(9.46f, 0.25f, 15.85f, 1f);
                    FlareSize = 1.1f;
                    IsDoorMalfunction = false;
                    break;
                case FlareIntensity.Average:
                    ScreenDistortionIntensity = 0.8f;
                    RadioDistortionIntensity = 0.6f;
                    RadioBreakthroughLength = 0.5f;
                    RadioFrequencyShift = 50f;
                    AuroraColor1 = new Color(0.38f, 6.88f, 0f, 1f);
                    AuroraColor2 = new Color(15.55f, 0.83f, 7.32f, 1f);
                    FlareSize = 1.25f;
                    IsDoorMalfunction = true;
                    break;
                case FlareIntensity.Strong:
                    ScreenDistortionIntensity = 1f;
                    RadioDistortionIntensity = 0.85f;
                    RadioBreakthroughLength = 0.25f;
                    RadioFrequencyShift = 10f;
                    AuroraColor1 = new Color(5.92f, 0f, 11.98f, 1f);
                    AuroraColor2 = new Color(8.65f, 0.83f, 1.87f, 1f);
                    FlareSize = 1.4f;
                    IsDoorMalfunction = true;
                    break;
            }
        }
    }
}