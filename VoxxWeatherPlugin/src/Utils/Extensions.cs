using System.Collections.Generic;
using UnityEngine;
using VoxxWeatherPlugin.Weathers;
using UnityEngine.Rendering;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections;
using System.Threading.Tasks;
using Unity.AI.Navigation;

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
        internal static IEnumerator BakeMasks(this Texture2DArray snowMasks,
                                            List<GameObject> objectsToBake,
                                            Material bakeMaterial,
                                            int bakeResolution = 1024,
                                            int submeshIndex = 0
                                            )
        {
            if (objectsToBake.Count == 0)
            {
                Debug.LogDebug("No objects to bake!");
                yield break;
            }
            if (snowMasks.depth != objectsToBake.Count)
            {
                Debug.LogError("The depth of the snowMasks texture array must be equal to the number of objects to bake!");
                yield break;
            }

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

            RenderTexture tempRT = RenderTexture.GetTemporary(bakeResolution, bakeResolution, 0, RenderTextureFormat.ARGBFloat);
            tempRT.wrapMode = TextureWrapMode.Clamp;
            tempRT.filterMode = FilterMode.Trilinear;

            RenderTexture blurRT1 = RenderTexture.GetTemporary(bakeResolution, bakeResolution, 0, RenderTextureFormat.ARGBFloat);
            blurRT1.wrapMode = TextureWrapMode.Clamp;
            blurRT1.filterMode = FilterMode.Trilinear;

            RenderTexture blurRT2 = RenderTexture.GetTemporary(bakeResolution, bakeResolution, 0, RenderTextureFormat.ARGBFloat);
            blurRT2.wrapMode = TextureWrapMode.Clamp;
            blurRT2.filterMode = FilterMode.Trilinear;
            
            Texture2D maskLayer = new Texture2D(bakeResolution, bakeResolution, TextureFormat.RGBAFloat, false);
            maskLayer.wrapMode = TextureWrapMode.Clamp;
            maskLayer.filterMode = FilterMode.Trilinear;

            for (int textureIndex = 0; textureIndex < objectsToBake.Count; textureIndex++)
            {
                sw.Restart();
                GameObject objectToBake = objectsToBake[textureIndex];
                Mesh? mesh = objectToBake.GetComponent<MeshFilter>()?.sharedMesh;

                if (mesh == null)
                {
                    Debug.LogError($"No mesh found on object to bake with name: {objectToBake.name}!");
                    yield break;
                }

                Debug.LogDebug("Baking mask for " + objectToBake.name + " with texture index " + textureIndex + " and submesh index " + submeshIndex);

                RenderTexture currentRT = RenderTexture.active;
                RenderTexture.active = tempRT;
                GL.Clear(true, true, Color.clear);
                
                var matrix = objectToBake.transform.localToWorldMatrix;
                // UV1 is used for baking here (see shader implementation)
                if (bakeMaterial?.SetPass(0) ?? false)
                    Graphics.DrawMeshNow(mesh, matrix, submeshIndex);

                sw.Stop();
                Debug.LogDebug("Baking took " + sw.ElapsedMilliseconds + " ms");
                sw.Restart();
                // Blur the normal map horizontally
                Graphics.Blit(tempRT, blurRT1, bakeMaterial, 1);
                // Blur the normal map vertically
                Graphics.Blit(blurRT1, blurRT2, bakeMaterial, 2);

                RenderTexture.active = blurRT2;
                maskLayer.ReadPixels(new Rect(0, 0, bakeResolution, bakeResolution), 0, 0);

                // Copy the texture to the specified index in the masks texture array
                Graphics.CopyTexture(maskLayer, 0, 0, snowMasks, textureIndex, 0);
                
                sw.Stop();
                Debug.LogDebug("Blurring took " + sw.ElapsedMilliseconds + " ms");

    #if DEBUG

                // *** DEBUG: Display blurRT2 on the screen ***
                GameObject debugQuad = new GameObject("DebugQuad");
                debugQuad.transform.position = 5 * (textureIndex + 1) *Vector3.up; // Position as needed
                debugQuad.transform.localScale = new Vector3(5f, 5f, 5f);
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

                Texture2D debugTexture = new Texture2D(bakeResolution, bakeResolution, TextureFormat.RGBAFloat, false);
                Graphics.CopyTexture(snowMasks, textureIndex, 0, debugTexture, 0, 0);
                debugTexture.Apply();

                Material debugMaterial = new Material(Shader.Find("HDRP/Unlit"));
                debugMaterial.mainTexture = debugTexture;
                renderer.sharedMaterial = debugMaterial;
    #endif        

                RenderTexture.active = currentRT;

                yield return null;
            }

            
            RenderTexture.ReleaseTemporary(tempRT);
            RenderTexture.ReleaseTemporary(blurRT1);
            RenderTexture.ReleaseTemporary(blurRT2);
            GameObject.Destroy(maskLayer);

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

        public static void WhiteOut(this RenderTexture rt)
        {
            // Save the currently active render texture
            RenderTexture previous = RenderTexture.active;
            // Set the new render texture as active
            RenderTexture.active = rt;
            // Clear the render texture to white
            GL.Clear(true, true, Color.white);
            // Restore the previously active render texture
            RenderTexture.active = previous;
        }

        public static IEnumerator AsCoroutine(this Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Debug.LogError("Task failed: " + task.Exception);
                yield break;
            }
        }

        public static IEnumerator NavMeshRebuildCoroutine(this NavMeshSurface navMeshSurface, AsyncOperation asyncOperation, System.Diagnostics.Stopwatch sw)
        {
            sw.Restart();

            while (!asyncOperation.isDone)
                yield return null;

            sw.Stop();
            
            Debug.LogDebug($"NavMesh rebaked in {sw.ElapsedMilliseconds} ms");
        }
    
    }
}
