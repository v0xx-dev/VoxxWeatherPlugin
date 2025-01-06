using System.Collections.Generic;
using UnityEngine;
using VoxxWeatherPlugin.Weathers;
using UnityEngine.Rendering;
using Unity.Netcode;
using UnityEngine.UI;

namespace VoxxWeatherPlugin.Utils
{
    public static class RandomExtensions
    {
        // Extension method for System.Random
        public static float NextDouble(this System.Random random, float min, float max)
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

        /// <summary>
        /// Bakes a mask texture for the object to be used with Snowfall shaders.
        /// </summary>
        /// <param name="objectToBake"></param> The GameObject to bake the mask for.
        /// <param name="snowfallData"></param> The SnowfallData object containing the bake material, resolution, etc.
        /// <param name="submeshIndex"></param> The index of the submesh to bake the mask for.
        /// <returns></returns> The baked mask texture.
        internal static Texture2D? BakeMask(this GameObject objectToBake,
                                            Material bakeMaterial,
                                            Texture2DArray snowMasks,
                                            int bakeResolution = 1024,
                                            int textureIndex = -1,
                                            int submeshIndex = 0
                                            )
        {
            Mesh mesh = objectToBake.GetComponent<MeshFilter>().sharedMesh;

            if (mesh == null)
            {
                Debug.LogError("No mesh found on object to bake!");
                return null;
            }

            Debug.LogDebug("Baking mask for " + objectToBake.name + " with texture index " + textureIndex + " and submesh index " + submeshIndex);

            RenderTexture tempRT = RenderTexture.GetTemporary(bakeResolution, bakeResolution, 0, RenderTextureFormat.ARGBFloat);
            tempRT.wrapMode = TextureWrapMode.Clamp;
            tempRT.filterMode = FilterMode.Trilinear;

            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = tempRT;
            GL.Clear(true, true, Color.clear);
            
            var matrix = objectToBake.transform.localToWorldMatrix;
            // UV1 is used for baking here (see shader implementation)
            if (bakeMaterial?.SetPass(0) ?? false)
                Graphics.DrawMeshNow(mesh, matrix, submeshIndex);

            RenderTexture blurRT1 = RenderTexture.GetTemporary(bakeResolution, bakeResolution, 0, RenderTextureFormat.ARGBFloat);
            blurRT1.wrapMode = TextureWrapMode.Clamp;
            blurRT1.filterMode = FilterMode.Trilinear;

            RenderTexture blurRT2 = RenderTexture.GetTemporary(bakeResolution, bakeResolution, 0, RenderTextureFormat.ARGBFloat);
            blurRT2.wrapMode = TextureWrapMode.Clamp;
            blurRT2.filterMode = FilterMode.Trilinear;

            // Blur the normal map horizontally
            Graphics.Blit(tempRT, blurRT1, bakeMaterial, 1);
            // Blur the normal map vertically
            Graphics.Blit(blurRT1, blurRT2, bakeMaterial, 2);

            RenderTexture.active = blurRT2;
            Texture2D maskTexture = new Texture2D(bakeResolution, bakeResolution, TextureFormat.RGBAFloat, false);
            maskTexture.wrapMode = TextureWrapMode.Clamp;
            maskTexture.filterMode = FilterMode.Trilinear;
            maskTexture.ReadPixels(new Rect(0, 0, bakeResolution, bakeResolution), 0, 0);

 #if DEBUG

            // *** DEBUG: Display blurRT2 on the screen ***
            GameObject debugQuad = new GameObject("DebugQuad");
            debugQuad.transform.position = 5*Vector3.up; // Position as needed
            MeshRenderer renderer = debugQuad.AddComponent<MeshRenderer>();
            MeshFilter filter = debugQuad.AddComponent<MeshFilter>();

            Mesh quadMesh = new Mesh();
            quadMesh.vertices = new Vector3[] {
                new Vector3(-1, -1, 0),
                new Vector3(1, -1, 0),
                new Vector3(1, 1, 0),
                new Vector3(-1, 1, 0)
            };
            quadMesh.uv = new Vector2[] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            quadMesh.RecalculateNormals();
            quadMesh.RecalculateBounds();
            filter.mesh = quadMesh;

            Material debugMaterial = new Material(Shader.Find("HDRP/Unlit"));
            debugMaterial.mainTexture = maskTexture;
            renderer.sharedMaterial = debugMaterial;
#endif        

            if (textureIndex != -1) // Copy the texture to the specified index in the masks texture array
            {
                Graphics.CopyTexture(maskTexture, 0, 0, snowMasks, textureIndex, 0);
                Object.Destroy(maskTexture);
            }
            else
            {
                maskTexture.Apply(false);
            }
            
            RenderTexture.active = currentRT;
            RenderTexture.ReleaseTemporary(tempRT);
            RenderTexture.ReleaseTemporary(blurRT1);
            RenderTexture.ReleaseTemporary(blurRT2);

            return maskTexture;
        }
   
        public static GameObject Duplicate(this GameObject original, bool disableShadows = true, bool removeCollider = true, bool noChildren = true)
        {
            // Temporarily unparent children
            List<Transform> children = new List<Transform>();
            if (noChildren)
            {
                foreach (Transform child in original.transform)
                {
                    children.Add(child);
                    child.SetParent(null);
                }
            }
            // Instantiate but without children of original
            GameObject duplicate = GameObject.Instantiate(original);
            
            // Reparent children to the original
            if (noChildren)
            {
                foreach (Transform child in children)
                {
                    child.SetParent(original.transform);
                }
            }

            duplicate.name = original.name + "_Copy";
            // Copy transform and hierarchy
            duplicate.transform.SetParent(original.transform.parent);
            duplicate.transform.localPosition = original.transform.localPosition;
            duplicate.transform.localRotation = original.transform.localRotation;
            duplicate.transform.localScale = original.transform.localScale;

            // Disable shadows for all renderers in the duplicate
            MeshRenderer[] renderers = duplicate.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            
            if (!disableShadows)
            {
                // Disable shadows for the original object
                MeshRenderer? renderer = original.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }

                // Duplicate will cast shadows
                renderer = duplicate.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
            }

            if (removeCollider)
            {
                Collider[] colliders = duplicate.GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders)
                {
                    GameObject.Destroy(collider);
                }
            }

            return duplicate;
        }

        public static void RestoreShader(this Material material)
        {
            Shader m_Shader = Shader.Find(material.shader.name); 
            material.shader = m_Shader;
        }
        
        public static void SpawnAtPosition(this Item itemToSpawn, Vector3 spawnPosition, int scrapValue = 7)
        {
            if (itemToSpawn == null)
            {
                Debug.LogError("Error: Item to spawn is null.");
                return;
            }

            // Instantiate the item prefab
            GameObject spawnedItemObject = GameObject.Instantiate(itemToSpawn.spawnPrefab, spawnPosition, Quaternion.identity);

            // Get the GrabbableObject component and set its properties
            GrabbableObject grabbableObject = spawnedItemObject.GetComponent<GrabbableObject>();
            grabbableObject.transform.rotation = Quaternion.Euler(grabbableObject.itemProperties.restingRotation);
            grabbableObject.fallTime = 0f;

            // Set a specific scrap value
            grabbableObject.scrapValue = scrapValue;

            // Spawn the object on the network
            NetworkObject networkObject = spawnedItemObject.GetComponent<NetworkObject>();
            networkObject.Spawn();
        }

        public static string CleanMoonName(this string moonName)
        {
            return moonName.Replace(" ", "").Replace("_", "").Replace("-", "").ToLower();
        }
    
    }
}
