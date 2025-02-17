using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System;
using System.Collections.Generic;
using UnityEditor;

public class CameraDebugSettings : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    private HDAdditionalCameraData cameraData;

    // Individual boolean fields for each FrameSettingsField
    [Header("After Post-processing")]
    public bool afterPostprocess = false;
    
    [Header("Anti-aliasing")]
    public bool alphaToMask = false;
    public bool antialiasing = false;
    
    [Header("Rendering")]
    public bool asymmetricProjection = false;
    public bool asyncCompute = false;
    public bool atmosphericScattering = false;
    public bool bigTilePrepass = false;
    
    [Header("Post-processing Effects")]
    public bool bloom = false;
    public bool chromaticAberration = false;
    public bool colorGrading = false;
    public bool depthOfField = false;
    public bool dithering = false;
    public bool exposureControl = false;
    public bool filmGrain = false;
    public bool lensDistortion = false;
    public bool lensFlareDataDriven = false;
    public bool motionBlur = false;
    public bool paniniProjection = false;
    public bool stopNaN = false;
    public bool tonemapping = false;
    public bool vignette = false;
    
    [Header("Buffer Handling")]
    public bool clearGBuffers = false;
    
    [Header("Lighting")]
    public bool computeLightEvaluation = false;
    public bool computeLightVariants = false;
    public bool computeMaterialVariants = false;
    public bool contactShadows = false;
    public bool contactShadowsAsync = false;
    public bool directSpecularLighting = false;
    public bool fptlForForwardOpaque = false;
    public bool lightLayers = false;
    public bool lightListAsync = false;
    
    [Header("Shadows")]
    public bool screenSpaceShadows = false;
    public bool shadowMaps = false;
    public bool shadowmask = false;
    
    [Header("Reflections & Refraction")]
    public bool distortion = false;
    public bool refraction = false;
    public bool roughDistortion = false;
    public bool roughRefraction = false;
    public bool skyReflection = false;
    public bool ssr = false;
    public bool ssrAsync = false;
    
    [Header("Ambient Occlusion & GI")]
    public bool ssao = false;
    public bool ssaoAsync = false;
    public bool ssgi = false;
    
    [Header("Custom Passes")]
    public bool customPass = false;
    public bool customPostProcess = false;
    
    [Header("Decals")]
    public bool decalLayers = false;
    public bool decals = false;
    
    [Header("Transparency")]
    public bool lowResTransparent = false;
    public bool transparentObjects = false;
    public bool transparentPostpass = false;
    public bool transparentPrepass = false;
    public bool transparentSSR = false;
    public bool transparentsWriteMotionVector = false;
    
    [Header("Motion Vectors")]
    public bool motionVectors = false;
    public bool objectMotionVectors = false;
    
    [Header("Level of Detail")]
    public bool lodBias = false;
    public bool lodBiasMode = false;
    public bool lodBiasQualityLevel = false;
    public bool maximumLODLevel = false;
    public bool maximumLODLevelMode = false;
    public bool maximumLODLevelQualityLevel = false;
    
    [Header("Shader Quality")]
    public bool litShaderMode = false;
    public bool materialQualityLevel = false;
    
    [Header("MSAA")]
    public bool msaa = false;
    public bool msaaMode = false;
    
    [Header("Subsurface Scattering")]
    public bool subsurfaceScattering = false;
    public bool sssCustomSampleBudget = false;
    public bool sssQualityLevel = false;
    public bool sssQualityMode = false;
    public bool transmission = false;
    
    [Header("Volumetrics")]
    public bool normalizeReflectionProbeWithProbeVolume = false;
    public bool probeVolume = false;
    public bool reprojectionForVolumetrics = false;
    public bool volumeVoxelizationsAsync = false;
    public bool volumetricClouds = false;
    public bool fullResolutionCloudsForSky = false;
    public bool volumetrics = false;
    
    [Header("Probes")]
    public bool planarProbe = false;
    public bool reflectionProbe = false;
    public bool replaceDiffuseForIndirect = false;
    
    [Header("Ray Tracing")]
    public bool rayTracing = false;
    
    [Header("Rendering Features")]
    public bool depthPrepassWithDeferredRendering = false;
    public bool deferredTile = false;
    public bool opaqueObjects = false;
    public bool screenCoordOverride = false;
    public bool virtualTexturing = false;
    public bool water = false;
    public bool zTestAfterPostProcessTAA = false;

    private Dictionary<FrameSettingsField, bool> settingsState = new Dictionary<FrameSettingsField, bool>();
    private Dictionary<FrameSettingsField, System.Reflection.FieldInfo> fieldInfoMap = new Dictionary<FrameSettingsField, System.Reflection.FieldInfo>();

    private void OnEnable()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
        {
            Debug.LogError("No camera assigned or found on this GameObject");
            enabled = false;
            return;
        }

        cameraData = targetCamera.GetComponent<HDAdditionalCameraData>();
        if (cameraData == null)
        {
            Debug.LogError("No HDAdditionalCameraData found on the camera");
            enabled = false;
            return;
        }

        // Map enum values to field infos
        MapFieldsToEnums();

        // Initialize the dictionary and boolean fields with current settings
        InitializeSettingsState();
    }

    private void MapFieldsToEnums()
    {
        // Get all fields in this class
        var fields = GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(bool))
            {
                // Try to convert the field name to the corresponding enum value
                if (Enum.TryParse(field.Name, true, out FrameSettingsField enumValue))
                {
                    fieldInfoMap[enumValue] = field;
                }
            }
        }
    }

    private void InitializeSettingsState()
    {
        // Get all enum values
        Array fields = Enum.GetValues(typeof(FrameSettingsField));

        // Skip the 'None' value which is at index 0
        for (int i = 1; i < fields.Length; i++)
        {
            FrameSettingsField field = (FrameSettingsField)fields.GetValue(i);
            bool isEnabled = cameraData.renderingPathCustomFrameSettings.IsEnabled(field);
            bool isOverridden = cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field];

            settingsState[field] = isEnabled;

            // Update the corresponding boolean field if we have a mapping
            if (fieldInfoMap.TryGetValue(field, out var fieldInfo))
            {
                fieldInfo.SetValue(this, isEnabled);
            }
        }
    }

    public void SetOverride(FrameSettingsField setting, bool enabled)
    {
        cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)setting] = true;
        cameraData.renderingPathCustomFrameSettings.SetEnabled(setting, enabled);
        settingsState[setting] = enabled;

        // Update the corresponding boolean field if we have a mapping
        if (fieldInfoMap.TryGetValue(setting, out var fieldInfo))
        {
            fieldInfo.SetValue(this, enabled);
        }
    }

    public bool GetSettingState(FrameSettingsField setting)
    {
        if (settingsState.TryGetValue(setting, out bool value))
            return value;
        
        return false;
    }

    public void ToggleSetting(FrameSettingsField setting)
    {
        bool currentState = GetSettingState(setting);
        SetOverride(setting, !currentState);
    }

    public void ApplyAllSettings()
    {
        // Apply all boolean field values to the camera settings
        foreach (var pair in fieldInfoMap)
        {
            FrameSettingsField setting = pair.Key;
            System.Reflection.FieldInfo fieldInfo = pair.Value;
            bool enabled = (bool)fieldInfo.GetValue(this);
            
            SetOverride(setting, enabled);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CameraDebugSettings))]
    public class CameraDebugSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CameraDebugSettings settings = (CameraDebugSettings)target;
            if (settings.cameraData == null)
                return;

            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Apply All Settings"))
            {
                settings.ApplyAllSettings();
                EditorUtility.SetDirty(target);
            }
            
            if (GUILayout.Button("Reset All Overrides"))
            {
                if (EditorUtility.DisplayDialog("Reset All Overrides", 
                    "Are you sure you want to reset all frame setting overrides?", 
                    "Yes", "No"))
                {
                    ResetAllOverrides(settings);
                    EditorUtility.SetDirty(target);
                }
            }
        }
        
        private void ResetAllOverrides(CameraDebugSettings settings)
        {
            Array fields = Enum.GetValues(typeof(FrameSettingsField));
            for (int i = 1; i < fields.Length; i++) // Skip None at index 0
            {
                FrameSettingsField field = (FrameSettingsField)fields.GetValue(i);
                settings.cameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = false;
            }
            settings.InitializeSettingsState();
        }
    }
#endif

    // Unity callback - apply settings when values change in play mode
    private void OnValidate()
    {
        if (Application.isPlaying && cameraData != null)
        {
            ApplyAllSettings();
        }
    }

    public void SyncResolution()
    {
        // Sync cameras resolution with the target texture
        if (targetCamera != null && targetCamera.targetTexture != null)
        {
            targetCamera.allowDynamicResolution = true;
            targetCamera.targetTexture = targetCamera.targetTexture;
            targetCamera.pixelRect = new Rect(0, 0, targetCamera.targetTexture.width, targetCamera.targetTexture.height);
        }
    }
}