using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

namespace VoxxWeatherPlugin.Behaviours
{
    /// <summary>
    /// Camera parameter binding helper class with render texture support.
    /// </summary>
    [VFXBinder("HDRP/HDRP Texture&Camera")]
    public class HDRPCameraOrTextureBinder : VFXBinderBase
    {
        /// <summary>
        /// Camera HDRP additional data.
        /// </summary>
        public HDAdditionalCameraData AdditionalData;
        public RenderTexture? depthTexture;
        public RenderTexture? colorTexture;
        bool useCameraBuffer = false;
        internal Camera m_Camera;

        [VFXPropertyBinding("UnityEditor.VFX.CameraType"), SerializeField]
        ExposedProperty CameraProperty = "Camera";

        RTHandle m_Texture;

        ExposedProperty m_Position;
        ExposedProperty m_Angles;
        ExposedProperty m_Scale;
        ExposedProperty m_FieldOfView;
        ExposedProperty m_NearPlane;
        ExposedProperty m_FarPlane;
        ExposedProperty m_AspectRatio;
        ExposedProperty m_Dimensions;
        ExposedProperty m_ScaledDimensions;
        ExposedProperty m_DepthBuffer;
        ExposedProperty m_ColorBuffer;
        ExposedProperty m_OrthographicSize;

        /// <summary>
        /// Set a camera property.
        /// </summary>
        /// <param name="name">Property name.</param>
        public void SetCameraProperty(string name)
        {
            CameraProperty = name;
            UpdateSubProperties();
        }

        void UpdateSubProperties()
        {
            // Get Camera component from HDRP additional data
            if (AdditionalData != null)
            {
                m_Camera = AdditionalData.GetComponent<Camera>();
            }

            // Update VFX Sub Properties
            m_Position = CameraProperty + "_transform_position";
            m_Angles = CameraProperty + "_transform_angles";
            m_Scale = CameraProperty + "_transform_scale";
            m_FieldOfView = CameraProperty + "_fieldOfView";
            m_NearPlane = CameraProperty + "_nearPlane";
            m_FarPlane = CameraProperty + "_farPlane";
            m_AspectRatio = CameraProperty + "_aspectRatio";
            m_Dimensions = CameraProperty + "_pixelDimensions";
            m_ScaledDimensions = CameraProperty + "_scaledPixelDimensions";
            m_DepthBuffer = CameraProperty + "_depthBuffer";
            m_ColorBuffer = CameraProperty + "_colorBuffer";
            m_OrthographicSize = CameraProperty + "_orthographicSize";
        }

        void RequestHDRPBuffersAccess(ref HDAdditionalCameraData.BufferAccess access)
        {
            access.RequestAccess(HDAdditionalCameraData.BufferAccessType.Color);
            access.RequestAccess(HDAdditionalCameraData.BufferAccessType.Depth);
        }

        /// <summary>
        /// OnEnable implementation.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            if (AdditionalData != null)
                AdditionalData.requestGraphicsBuffer += RequestHDRPBuffersAccess;

            UpdateSubProperties();
        }

        /// <summary>
        /// OnDisable implementation.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            if (AdditionalData != null)
                AdditionalData.requestGraphicsBuffer -= RequestHDRPBuffersAccess;
        }

        private void OnValidate()
        {
            UpdateSubProperties();

            if (AdditionalData != null)
                AdditionalData.requestGraphicsBuffer += RequestHDRPBuffersAccess;
        }

        /// <summary>
        /// Returns true if the Visual Effect and the configuration of the binder are valid to perform the binding.
        /// </summary>
        /// <param name="component">Component to be tested.</param>
        /// <returns>True if the Visual Effect and the configuration of the binder are valid to perform the binding.</returns>
        public override bool IsValid(VisualEffect component)
        {
            return AdditionalData != null
                && m_Camera != null
                && component.HasVector3(m_Position)
                && component.HasVector3(m_Angles)
                && component.HasVector3(m_Scale)
                && component.HasFloat(m_FieldOfView)
                && component.HasFloat(m_NearPlane)
                && component.HasFloat(m_FarPlane)
                && component.HasFloat(m_AspectRatio)
                && component.HasVector2(m_Dimensions)
                && component.HasVector2(m_ScaledDimensions)
                && component.HasTexture(m_DepthBuffer)
                && component.HasTexture(m_ColorBuffer)
                && component.HasFloat(m_OrthographicSize);
        }

        /// <summary>
        /// Update bindings for a visual effect.
        /// </summary>
        /// <param name="component">Component to update.</param>
        public override void UpdateBinding(VisualEffect component)
        {
            // Prioritize textures over camera buffers
            bool useDepthTexture = depthTexture != null;
            bool useColorTexture = colorTexture != null;

            if (!useDepthTexture && !useColorTexture && !useCameraBuffer)
            {
                Debug.LogWarning("No texture or camera buffer selected for HDRP Camera or Texture Binder.");
                return;
            }

            RTHandle? depth = null;
            RTHandle? color = null;

            if (!useDepthTexture && useCameraBuffer)
            {
                depth = AdditionalData.GetGraphicsBuffer(HDAdditionalCameraData.BufferAccessType.Depth);
            }
            
            if (!useColorTexture && useCameraBuffer)
            {
                color = AdditionalData.GetGraphicsBuffer(HDAdditionalCameraData.BufferAccessType.Color);
            }

            if (depth == null && depthTexture == null && color == null && colorTexture == null)
                return;

            component.SetVector3(m_Position, AdditionalData.transform.position);
            component.SetVector3(m_Angles, AdditionalData.transform.eulerAngles);
            component.SetVector3(m_Scale, AdditionalData.transform.lossyScale);

            // While field of View is set in degrees for the camera, it is expected in radians in VFX
            component.SetFloat(m_FieldOfView, Mathf.Deg2Rad * m_Camera.fieldOfView);
            component.SetFloat(m_NearPlane, m_Camera.nearClipPlane);
            component.SetFloat(m_FarPlane, m_Camera.farClipPlane);
            component.SetFloat(m_OrthographicSize, m_Camera.orthographicSize);
            
            if (useDepthTexture)
            {
                component.SetVector2(m_Dimensions, new Vector2(depthTexture!.width, depthTexture.height));
                component.SetVector2(m_ScaledDimensions, new Vector2(depthTexture.width, depthTexture.height));
                component.SetFloat(m_AspectRatio, (float)depthTexture.width / (float)depthTexture.height);
            }
            else if (useColorTexture)
            {
                component.SetVector2(m_Dimensions, new Vector2(colorTexture!.width, colorTexture.height));
                component.SetVector2(m_ScaledDimensions, new Vector2(colorTexture.width, colorTexture.height));
                component.SetFloat(m_AspectRatio, (float)colorTexture.width / (float)colorTexture.height);
            }
            else if (depth != null)
            {
                component.SetVector2(m_Dimensions, new Vector2(m_Camera.pixelWidth * depth.rtHandleProperties.rtHandleScale.x, m_Camera.pixelHeight * depth.rtHandleProperties.rtHandleScale.y));
                component.SetVector2(m_ScaledDimensions, new Vector2(m_Camera.pixelWidth * depth.rtHandleProperties.rtHandleScale.x, m_Camera.pixelHeight * depth.rtHandleProperties.rtHandleScale.y));
                component.SetFloat(m_AspectRatio, m_Camera.aspect);
            }
            
            if (useDepthTexture)
                component.SetTexture(m_DepthBuffer, depthTexture);
            else if (depth != null)
                component.SetTexture(m_DepthBuffer, depth!.rt);

            if (useColorTexture)
                component.SetTexture(m_ColorBuffer, colorTexture);
            else if (color != null)
                component.SetTexture(m_ColorBuffer, color!.rt);

        }

        /// <summary>
        /// To string implementation.
        /// </summary>
        /// <returns>String containing the binder information.</returns>
        public override string ToString()
        {
            return string.Format($"HDRP Camera : '{(AdditionalData == null? "null" : AdditionalData.gameObject.name)}' -> {CameraProperty}");
        }
    }

    [AddComponentMenu("VFX/Property Binders/Box Collider Binder")]
    [VFXBinder("Collider/Box")]
    class VFXBoxBinder : VFXBinderBase
    {
        public string Property { get { return (string)m_Property; } set { m_Property = value; UpdateSubProperties(); } }

        [VFXPropertyBinding("UnityEditor.VFX.AABox"), SerializeField]
        protected ExposedProperty m_Property = "AABox";
        public BoxCollider Target = null;

        private ExposedProperty Center;
        private ExposedProperty Size;

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateSubProperties();
        }

        void OnValidate()
        {
            UpdateSubProperties();
        }

        void UpdateSubProperties()
        {
            Center = m_Property + "_center";
            Size = m_Property + "_size";
        }

        public override bool IsValid(VisualEffect component)
        {
            return Target != null && component.HasVector3(Center) && component.HasVector3(Size);
        }

        public override void UpdateBinding(VisualEffect component)
        {
            component.SetVector3(Center, Target.center);
            component.SetVector3(Size, Target.size);
        }


        public override string ToString()
        {
            return string.Format("Box : '{0}' -> {1}", m_Property, Target == null ? "(null)" : Target.name);
        }
    }
}