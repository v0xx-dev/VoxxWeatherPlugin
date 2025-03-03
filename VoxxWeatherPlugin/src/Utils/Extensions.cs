using System.Collections.Generic;
using UnityEngine;
using VoxxWeatherPlugin.Weathers;
using UnityEngine.Rendering;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections;
using System.Threading.Tasks;
using Unity.AI.Navigation;
using System.IO;

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

        internal static IEnumerator BakeMasks(this Texture2DArray snowMasks,
                                            List<GameObject> objectsToBake,
                                            Material bakeMaterial,
                                            bool bakeMipmaps = false,
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
                
                if (!TryBakeMask(snowMasks, objectsToBake[textureIndex], bakeMaterial, textureIndex, tempRT, blurRT1, blurRT2, maskLayer, sw, bakeResolution, submeshIndex))
                {
                    Debug.LogError("Failed to bake mask for object at index " + textureIndex);
                    yield break;
                }

                yield return null;
            }

            
            RenderTexture.ReleaseTemporary(tempRT);
            RenderTexture.ReleaseTemporary(blurRT1);
            RenderTexture.ReleaseTemporary(blurRT2);
            GameObject.Destroy(maskLayer);

            snowMasks.Apply(updateMipmaps: bakeMipmaps, makeNoLongerReadable: true); // Move to the GPU

            Debug.LogDebug("Snow shader masks baked!");
        }

        internal static bool TryBakeMask(this Texture2DArray snowMasks,
                                            GameObject objectToBake,
                                            Material bakeMaterial,
                                            int textureIndex,
                                            RenderTexture tempRT,
                                            RenderTexture blurRT1,
                                            RenderTexture blurRT2,
                                            Texture2D maskLayer,
                                            System.Diagnostics.Stopwatch sw,
                                            int bakeResolution = 1024,
                                            int submeshIndex = 0
                                            )
        {
            if (objectToBake == null)
            {
                Debug.LogDebug("Object to bake is null!");
                return false;
            }
            if (snowMasks.depth < textureIndex)
            {
                Debug.LogError("The depth of the snowMasks texture array must be greater than or equal to the object index!");
                return false;
            }

            Mesh? mesh = objectToBake.GetComponent<MeshFilter>()?.sharedMesh;

            if (mesh == null)
            {
                Debug.LogError($"No mesh found on object to bake with name: {objectToBake.name}!");
                return false;
            }

            sw.Restart();
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
            string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            maskLayer.SaveToFile(Path.Combine(assemblyPath, $"mask_{objectToBake.name}.png"));
#endif        

            RenderTexture.active = currentRT;

            return true;
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

        public static Color Clamp(this Color color, float min, float max)
        {
            return new Color(
                Mathf.Clamp(color.r, min, max),
                Mathf.Clamp(color.g, min, max),
                Mathf.Clamp(color.b, min, max),
                Mathf.Clamp(color.a, min, max)
            );
        }

        public static IEnumerable<Transform> GetParents(this Transform transform)
        {
            Transform? parent = transform.parent;
            while (parent != null)
            {
                yield return parent;
                parent = parent.parent;
            }
        }

        public static void SaveToFile(this RenderTexture rt, string filePath)
        {
            // Check for null RenderTexture
            if (rt == null)
            {
                Debug.LogError("RenderTexture is null. Cannot save to file.");
                return;
            }
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            byte[] bytes = ImageConversion.EncodeToPNG(tex);
            File.WriteAllBytes(filePath, bytes);
            Object.DestroyImmediate(tex); 
        }

        public static void SaveToFile(this Texture2D tex, string filePath)
        {
            // Check for null RenderTexture
            if (tex == null)
            {
                Debug.LogError("Texture is null. Cannot save to file.");
                return;
            }
            // Copy the texture to a new Texture2D
            Texture2D newTex = new Texture2D(tex.width, tex.height, tex.format, tex.mipmapCount > 1);
            newTex.SetPixels(tex.GetPixels());
            newTex.Apply();
            byte[] bytes = ImageConversion.EncodeToPNG(tex);
            File.WriteAllBytes(filePath, bytes);
            Object.Destroy(newTex); 
        }
    }
}
