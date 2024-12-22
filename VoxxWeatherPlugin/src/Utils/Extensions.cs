using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using TriangleNet.Geometry;
using TriangleNet.Meshing;
using TriangleNet.Smoothing;
using TriangleNet.Unity;
using VoxxWeatherPlugin.Weathers;
using UnityEngine.Rendering;
using UnityEngine.InputSystem.EnhancedTouch;
using Unity.Netcode;

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
        /// Postprocesses a mesh terrain by refining, smoothing, and retriangulating it within specified bounds.
        /// </summary>
        /// <param name="meshTerrainObject">The GameObject whose mesh will be modified.  A copy of this object with the modified mesh will be created.</param>
        /// <param name="levelBounds">The bounds within which the mesh should be processed, in world space.</param>
        /// <param name="snowfallData">The SnowfallData object containing the postprocessing parameters.</param>
        /// <remarks>
        /// This method creates a copy of the original GameObject and modifies its mesh. The original GameObject is not altered.
        /// The height axis of the mesh is automatically determined based on the object's transform.
        /// Triangles completely outside the specified bounds are preserved, while triangles intersecting the bounds are processed.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown if `meshTerrainObject` or `levelBounds` is null.</exception>
        internal static void PostprocessMeshTerrain(this GameObject meshTerrainObject, Bounds levelBounds, SnowfallWeather snowfallData)
        {
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

            if (meshTerrainObject == null || levelBounds == null)
            {
                Debug.LogError("Object to modify and level bounds must be supplied.");
            }

            Mesh originalMesh = meshTerrainObject!.GetComponent<MeshFilter>().sharedMesh;
            (Mesh newMesh, int submeshIndex) = meshTerrainObject.ExtractLargestSubmesh();
            newMesh.name = originalMesh.name + "_Postprocessed";
            
            Debug.LogDebug("Extracted submesh with " + newMesh.vertexCount + " vertices and " + newMesh.triangles.Length / 3 + " triangles" + " from submesh " + submeshIndex + " of " + meshTerrainObject.name);

            Bounds objectSpaceBounds;
            if (snowfallData.UseBounds)
            {
                // Transform bounds to object space
                objectSpaceBounds = new Bounds(
                    meshTerrainObject.transform.InverseTransformPoint(levelBounds.center),
                    meshTerrainObject.transform.InverseTransformVector(levelBounds.size)
                );
                //Ensure bounds are not negative (take absolute value)
                objectSpaceBounds.size = new Vector3(Mathf.Abs(objectSpaceBounds.size.x), Mathf.Abs(objectSpaceBounds.size.y), Mathf.Abs(objectSpaceBounds.size.z));
                //Check if object space bounds intersect with mesh bounds
                if (!newMesh.bounds.Intersects(objectSpaceBounds))
                {
                    Debug.LogDebug("Object's bounds do not intersect with level bounds. No vertices will be processed.");
                    return;
                }
            }
            else
            {
                objectSpaceBounds = newMesh.bounds;
            }

            Debug.LogDebug("Bounds for triangulation: " + objectSpaceBounds.center + " " + objectSpaceBounds.size);

            // Get the original mesh data
            Vector3[] vertices = newMesh.vertices;
            int[] triangles = newMesh.triangles;

            // Determine height axis
            int heightAxis = 0;

            Vector3 objectSpaceUpDirection = meshTerrainObject.transform.InverseTransformDirection(Vector3.up);
            objectSpaceUpDirection.x *= Mathf.Sign(meshTerrainObject.transform.lossyScale.x);
            objectSpaceUpDirection.y *= Mathf.Sign(meshTerrainObject.transform.lossyScale.y);
            objectSpaceUpDirection.z *= Mathf.Sign(meshTerrainObject.transform.lossyScale.z);

            if (Mathf.Abs(Vector3.Dot(objectSpaceUpDirection, Vector3.right)) > 0.5f)
            {
                heightAxis = 0;
            }
            else if (Mathf.Abs(Vector3.Dot(objectSpaceUpDirection, Vector3.up)) > 0.5f)
            {
                heightAxis = 1;
            }
            else if (Mathf.Abs(Vector3.Dot(objectSpaceUpDirection, Vector3.forward)) > 0.5f)
            {
                heightAxis = 2;
            }

            Debug.LogDebug("Object's up axis: " + heightAxis + ". Object's up direction: " + objectSpaceUpDirection);

            //Calculate max scale only taking the absolute horizontal scales into account, horizontal is everything not on the height axis
            float maxScale = Mathf.Max(Mathf.Abs(meshTerrainObject.transform.lossyScale[(heightAxis + 1)%3]), Mathf.Abs(meshTerrainObject.transform.lossyScale[(heightAxis + 2) % 3]));
            float maxEdgeLength = snowfallData.baseEdgeLength / maxScale;
            Debug.LogDebug("Max scale: " + maxScale + " Max edge length: " + maxEdgeLength + "m");

            // Create lists to store vertices and triangles within bounds
            HashSet<int> vertexIndicesInBounds = new HashSet<int>();
            Queue<(int, int, int)> trianglesToRefine = new Queue<(int, int, int)>();
            // Track unique vertices to avoid duplicates
            Dictionary<Vector3, int> uniqueVertices = new Dictionary<Vector3, int>();
            //Triangles that are outside bounds
            List<int> trianglesToKeep = new List<int>();
            //Edges within bounds
            Dictionary<EdgePair, int> edgeCounts = new Dictionary<EdgePair, int>();
            // Dictionaries to map Triangle.NET vertices to original mesh indices and vice versa
            Dictionary<int, int> poly2VertMap = new Dictionary<int, int>();
            Dictionary<int, int> vert2PolyMap = new Dictionary<int, int>();
            
            // Find vertices within bounds and triangles to keep
            // Keep triangles that are outside bounds
            Debug.LogDebug("Filtering vertices and triangles... Count: " + triangles.Length / 3 + " Vertices: " + vertices.Length);
            sw.Start();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int p1 = triangles[i];
                int p2 = triangles[i + 1];
                int p3 = triangles[i + 2];

                //find unique vertices and if found a duplicate vertex, get a reference to the original vertex
                (p1, p2, p3) = CheckForDuplicateVertices(uniqueVertices, vertices, p1, p2, p3);

                bool allVerticesInBounds = objectSpaceBounds.Contains(vertices[p1]) &&
                                            objectSpaceBounds.Contains(vertices[p2]) &&
                                            objectSpaceBounds.Contains(vertices[p3]);
            
                if (allVerticesInBounds)
                {
                    
                    // Store vertices within bounds
                    vertexIndicesInBounds.Add(p1);
                    vertexIndicesInBounds.Add(p2);
                    vertexIndicesInBounds.Add(p3);

                    // Store triangles within bounds
                    if (snowfallData.SubdivideMesh)
                    {
                        trianglesToRefine.Enqueue((p1, p2, p3));
                    }

                    // Count edges
                    UpdateEdgeCounts(edgeCounts, p1, p2);
                    UpdateEdgeCounts(edgeCounts, p2, p3);
                    UpdateEdgeCounts(edgeCounts, p3, p1);
                }
                else
                {
                    trianglesToKeep.Add(p1);
                    trianglesToKeep.Add(p2);
                    trianglesToKeep.Add(p3);
                }
            }
            sw.Stop();
            Debug.LogDebug("Vertex and triangle filtering/deduplication time: " + sw.ElapsedMilliseconds + "ms");
            Debug.LogDebug("Vertices in bounds: " + vertexIndicesInBounds.Count + " Triangles in bounds: " + trianglesToRefine.Count);

            // Allow addition of new vertices
            List<Vector3> newVertices = new List<Vector3>(vertices);
            List<Vector2> newUVs = new List<Vector2>(newMesh.uv);

            // Refine triangles
            if (snowfallData.SubdivideMesh)
            {
                Debug.LogDebug($"Refining {trianglesToRefine.Count} triangles...");
                sw.Restart();
                Dictionary<Vector3, int> midpointVertices = new Dictionary<Vector3, int>();
                while (trianglesToRefine.Count > 0)
                {
                    (int p1, int p2, int p3) = trianglesToRefine.Dequeue();

                    Vector3 v1 = newVertices[p1];
                    Vector3 v2 = newVertices[p2];
                    Vector3 v3 = newVertices[p3];

                    // Calculate edge lengths
                    float edge1Length2 = (v1 - v2).sqrMagnitude;
                    float edge2Length2 = (v2 - v3).sqrMagnitude;
                    float edge3Length2 = (v3 - v1).sqrMagnitude;

                    EdgePair edge1 = new EdgePair(p1, p2);
                    EdgePair edge2 = new EdgePair(p2, p3);
                    EdgePair edge3 = new EdgePair(p3, p1);

                    float maxEdgeLength2 = maxEdgeLength * maxEdgeLength;

                    // Check if all of the edges are longer than the maximum length and if none of them are on the boundary
                    bool refine = edge1Length2 > maxEdgeLength2 && edge2Length2 > maxEdgeLength2 && edge3Length2 > maxEdgeLength2;
                    bool onBoundary = edgeCounts.ContainsKey(edge1) && edgeCounts[edge1] == 1 ||
                                    edgeCounts.ContainsKey(edge2) && edgeCounts[edge2] == 1 ||
                                    edgeCounts.ContainsKey(edge3) && edgeCounts[edge3] == 1;

                    if (refine && !onBoundary)
                    {
                        Vector3 midpoint1 = (v1 + v2) / 2;
                        Vector3 midpoint2 = (v2 + v3) / 2;
                        Vector3 midpoint3 = (v3 + v1) / 2;

                        Vector2 uv1 = newUVs[p1];
                        Vector2 uv2 = newUVs[p2];
                        Vector2 uv3 = newUVs[p3];

                        // Add new vertices while keeping track of duplicates
                        int newVertInd1 = newVertices.Count;
                        if (!midpointVertices.ContainsKey(midpoint1))
                        {
                            midpointVertices[midpoint1] = newVertInd1;
                            vertexIndicesInBounds.Add(newVertInd1);
                            newVertices.Add(midpoint1);
                            newUVs.Add((uv1 + uv2) / 2);
                        }
                        else
                        {
                            newVertInd1 = midpointVertices[midpoint1];
                        }

                        int newVertInd2 = newVertices.Count;
                        if (!midpointVertices.ContainsKey(midpoint2))
                        {
                            midpointVertices[midpoint2] = newVertInd2;
                            vertexIndicesInBounds.Add(newVertInd2);
                            newVertices.Add(midpoint2);
                            newUVs.Add((uv2 + uv3) / 2);
                        }
                        else
                        {
                            newVertInd2 = midpointVertices[midpoint2];
                        }

                        int newVertInd3 = newVertices.Count;
                        if (!midpointVertices.ContainsKey(midpoint3))
                        {
                            midpointVertices[midpoint3] = newVertInd3;
                            vertexIndicesInBounds.Add(newVertInd3);
                            newVertices.Add(midpoint3);
                            newUVs.Add((uv3 + uv1) / 2);
                        }
                        else
                        {
                            newVertInd3 = midpointVertices[midpoint3];
                        }

                        // Add new triangles
                        trianglesToRefine.Enqueue((p1, newVertInd1, newVertInd3));
                        trianglesToRefine.Enqueue((newVertInd1, p2, newVertInd2));
                        trianglesToRefine.Enqueue((newVertInd2, p3, newVertInd3));
                        trianglesToRefine.Enqueue((newVertInd1, newVertInd2, newVertInd3));
                    }
                }
                sw.Stop();
                Debug.LogDebug("Refinement time: " + sw.ElapsedMilliseconds + "ms");
            }

            // Create Triangle.NET polygon input
            var polygon = new Polygon();
            // Add vertices within bounds to the polygon
            foreach (int i in vertexIndicesInBounds)
            {
                Vertex triNetVertex = newVertices[i].ToTriangleNetVertex(newUVs[i], heightAxis);
                polygon.Add(triNetVertex);
                poly2VertMap[polygon.Points.Count - 1] = i;
                vert2PolyMap[i] = polygon.Points.Count - 1;
            }

            // Add segments to enforce edges on the outer boundary
            if (snowfallData.constrainEdges)
            {
                foreach (var edge in edgeCounts)
                {
                    if (edge.Value == 1)
                    {
                        polygon.Add(new Segment(polygon.Points[vert2PolyMap[edge.Key.P1]], polygon.Points[vert2PolyMap[edge.Key.P2]]));
                    }
                }
            }

            // Configure triangulation options
            ConstraintOptions options = new ConstraintOptions() { ConformingDelaunay = true, SegmentSplitting = 2};
            QualityOptions quality = new QualityOptions() { MinimumAngle = 30.0f, SteinerPoints = snowfallData.SubdivideMesh ? -1 : 0};

            // Perform triangulation
            Debug.LogDebug($"Triangulating {polygon.Points.Count} vertices...");
            sw.Restart();
            var mesh2d = polygon.Triangulate(options, quality);
            sw.Stop();
            Debug.LogDebug("Triangulation time: " + sw.ElapsedMilliseconds + "ms");
            if (snowfallData.SmoothMesh)
            {
                sw.Restart();
                var smoother = new LaplacianSmoother(1f);
                smoother.Smooth(mesh2d, snowfallData.smoothingIterations);
                sw.Stop();
                Debug.LogDebug("Smoothing time: " + sw.ElapsedMilliseconds + "ms");
            }

            // Create new triangles list combining kept triangles and new triangulation
            List<int> newTriangles = new List<int>(trianglesToKeep);

            Debug.LogDebug("Modified triangles: " + mesh2d.Triangles.Count);

            // Convert Triangle.NET triangles back to Unity mesh triangles
            foreach (ITriangle triangle in mesh2d.Triangles)
            {

                int v0 = triangle.GetVertexID(0);
                int v1 = triangle.GetVertexID(1);
                int v2 = triangle.GetVertexID(2);

                // Use the Dictionary to get the original mesh indices
                // If the vertex is new add it to the list

                if (!poly2VertMap.ContainsKey(v0))
                {
                    poly2VertMap[v0] = newVertices.Count;
                    newVertices.Add(triangle.GetVertex(0).ToVector3(heightAxis));
                    newUVs.Add(triangle.GetVertex(0).UV);
                }
                if (!poly2VertMap.ContainsKey(v1))
                {
                    poly2VertMap[v1] = newVertices.Count;
                    newVertices.Add(triangle.GetVertex(1).ToVector3(heightAxis));
                    newUVs.Add(triangle.GetVertex(1).UV);
                }
                if (!poly2VertMap.ContainsKey(v2))
                {
                    poly2VertMap[v2] = newVertices.Count;
                    newVertices.Add(triangle.GetVertex(2).ToVector3(heightAxis));
                    newUVs.Add(triangle.GetVertex(2).UV);
                }

                int vertexIndex0 = poly2VertMap[v0];
                int vertexIndex1 = poly2VertMap[v1];
                int vertexIndex2 = poly2VertMap[v2];

                //Different winding order based on a height axis
                if (heightAxis == 1)
                {
                    newTriangles.Add(vertexIndex0);
                    newTriangles.Add(vertexIndex2);
                    newTriangles.Add(vertexIndex1);
                }
                else
                {
                    newTriangles.Add(vertexIndex0);
                    newTriangles.Add(vertexIndex1);
                    newTriangles.Add(vertexIndex2);
                }

            }
            Debug.LogDebug("Original UVs: " + newMesh.uv.Length + " New UVs: " + newUVs.Count);
            Debug.LogDebug("Original vertices: " + vertices.Length + " New vertices: " + newVertices.Count);

            newMesh.vertices = newVertices.ToArray();
            newMesh.triangles = newTriangles.ToArray();
            newMesh.uv2 = UnwrapUVs(newMesh.vertices, heightAxis, true);
            if (snowfallData.replaceUvs)
            {
                newMesh.uv = newMesh.uv2; // Replace UVs with unwrapped UVs (only acceptable on moons with no splatmaps)
            }
            else
            {
                newMesh.uv = newUVs.ToArray(); // use original interpolated UVs
            }
            
            newMesh.Optimize();
            newMesh.RecalculateNormals();
            newMesh.RecalculateTangents();
            newMesh.RecalculateBounds();

            MeshFilter meshFilter = meshTerrainObject.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = newMesh;

            MeshCollider meshCollider = meshTerrainObject.GetComponent<MeshCollider>();
            meshCollider.sharedMesh = newMesh;
        }

        private static Vector2[] UnwrapUVs(Vector3[] vertices, int upAxis, bool normalize)
        {
            Vector2[] uvs = new Vector2[vertices.Length];
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < vertices.Length; i++)
            {
                if (upAxis == 0)
                    uvs[i] = new Vector2(vertices[i].y, vertices[i].z);
                else if (upAxis == 1)
                    uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
                else
                    uvs[i] = new Vector2(vertices[i].x, vertices[i].y);

                min = Vector2.Min(min, uvs[i]);
                max = Vector2.Max(max, uvs[i]);
            }
            if (normalize)
            {
                Vector2 size = max - min;
                for (int i = 0; i < uvs.Length; i++)
                {
                    uvs[i] = new Vector2((uvs[i].x - min.x) / size.x, (uvs[i].y - min.y) / size.y);
                }
            }

            return uvs;

        }

        private static void UpdateEdgeCounts(Dictionary<EdgePair, int> edgeCounts, int p1, int p2)
        {
            EdgePair edge = new EdgePair(p1, p2);
            if (edgeCounts.ContainsKey(edge))
                edgeCounts[edge]++;
            else
                edgeCounts[edge] = 1;
        }

        private static (int, int, int) CheckForDuplicateVertices(Dictionary<Vector3, int> uniqueVertices, Vector3[] vertices, int p1, int p2, int p3)
        {
            if (!uniqueVertices.ContainsKey(vertices[p1]))
            {
                uniqueVertices[vertices[p1]] = p1;
            }
            else
            {
                p1 = uniqueVertices[vertices[p1]];
            }
            if (!uniqueVertices.ContainsKey(vertices[p2]))
            {
                uniqueVertices[vertices[p2]] = p2;
            }
            else
            {
                p2 = uniqueVertices[vertices[p2]];
            }
            if (!uniqueVertices.ContainsKey(vertices[p3]))
            {
                uniqueVertices[vertices[p3]] = p3;
            }
            else
            {
                p3 = uniqueVertices[vertices[p3]];
            }

            return (p1, p2, p3);
        }

        public static (Mesh submesh, int submeshIndex) ExtractLargestSubmesh(this GameObject meshObject)
        {
            Mesh mesh = meshObject.GetComponent<MeshFilter>().sharedMesh;
            Transform transform = meshObject.transform;
            
            Mesh meshCopy = mesh.MakeReadableCopy();

            var submeshCount = meshCopy.subMeshCount;
            if (submeshCount <= 1) return (meshCopy, 0);


            var largestSubmeshIndex = Enumerable.Range(0, submeshCount)
                .OrderByDescending(i => meshCopy.GetSubMesh(i).vertexCount)
                .First();

            var triangles = meshCopy.GetTriangles(largestSubmeshIndex);
            var usedVertices = new HashSet<int>(triangles);
            var oldToNewVertexMap = new Dictionary<int, int>();

            Vector3[] vertices = meshCopy.vertices;
            Vector2[] uv = meshCopy.uv;
            Vector3[] normals = meshCopy.normals;
            Vector4[] tangents = meshCopy.tangents;
            
            var newVertices = new List<Vector3>();
            var newNormals = meshCopy.normals.Length > 0 ? new List<Vector3>() : null;
            var newUVs = meshCopy.uv.Length > 0 ? new List<Vector2>() : null;
            var newTangents = meshCopy.tangents.Length > 0 ? new List<Vector4>() : null;

            foreach (var oldIndex in usedVertices)
            {
                oldToNewVertexMap[oldIndex] = newVertices.Count;
                newVertices.Add(transform.InverseTransformPoint(vertices[oldIndex]));
                if (newNormals != null) newNormals.Add(normals[oldIndex]);
                if (newUVs != null) newUVs.Add(uv[oldIndex]);
                if (newTangents != null) newTangents.Add(tangents[oldIndex]);
            }

            var newTriangles = triangles.Select(oldIndex => oldToNewVertexMap[oldIndex]).ToArray();

            var submesh = new Mesh
            {
                name = meshCopy.name + "_Submesh" + largestSubmeshIndex,
                vertices = newVertices.ToArray(),
                triangles = newTriangles,
                indexFormat = meshCopy.indexFormat
            };

            if (newNormals != null) submesh.normals = newNormals.ToArray();
            if (newUVs != null) submesh.uv = newUVs.ToArray();
            if (newTangents != null) submesh.tangents = newTangents.ToArray();

            submesh.RecalculateBounds();

            Object.Destroy(meshCopy);

            return (submesh, largestSubmeshIndex);
        }

        //Credit to Matty for this method
        public static Mesh MakeReadableCopy(this Mesh nonReadableMesh)
        {
            if (nonReadableMesh.isReadable)
                return nonReadableMesh;

            var meshCopy = new Mesh();
            meshCopy.indexFormat = nonReadableMesh.indexFormat;

            // Handle vertices
            nonReadableMesh.vertexBufferTarget = GraphicsBuffer.Target.Vertex;
            if (nonReadableMesh.vertexBufferCount > 0)
            {
                var verticesBuffer = nonReadableMesh.GetVertexBuffer(0);
                var totalSize = verticesBuffer.stride * verticesBuffer.count;
                var data = new byte[totalSize];
                verticesBuffer.GetData(data);
                meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, nonReadableMesh.GetVertexAttributes());
                meshCopy.SetVertexBufferData(data, 0, 0, totalSize);
                verticesBuffer.Release();
            }

            // Handle triangles
            nonReadableMesh.indexBufferTarget = GraphicsBuffer.Target.Index;
            meshCopy.subMeshCount = nonReadableMesh.subMeshCount;
            var indexesBuffer = nonReadableMesh.GetIndexBuffer();
            var tot = indexesBuffer.stride * indexesBuffer.count;
            var indexesData = new byte[tot];
            indexesBuffer.GetData(indexesData);
            meshCopy.SetIndexBufferParams(indexesBuffer.count, nonReadableMesh.indexFormat);
            meshCopy.SetIndexBufferData(indexesData, 0, 0, tot);
            indexesBuffer.Release();

            // Restore submesh structure
            uint currentIndexOffset = 0;
            for (var i = 0; i < meshCopy.subMeshCount; i++)
            {
                var subMeshIndexCount = nonReadableMesh.GetIndexCount(i);
                meshCopy.SetSubMesh(i, new SubMeshDescriptor((int)currentIndexOffset, (int)subMeshIndexCount));
                currentIndexOffset += subMeshIndexCount;
            }

            // Recalculate normals and bounds
            meshCopy.RecalculateNormals();
            meshCopy.RecalculateBounds();

            meshCopy.name = $"Readable {nonReadableMesh.name}";
            return meshCopy;
        }


        /// <summary>
        /// Bakes a mask texture for the object to be used with Snowfall shaders.
        /// </summary>
        /// <param name="objectToBake"></param> The GameObject to bake the mask for.
        /// <param name="snowfallData"></param> The SnowfallData object containing the bake material, resolution, etc.
        /// <param name="submeshIndex"></param> The index of the submesh to bake the mask for.
        /// <returns></returns> The baked mask texture.
        internal static Texture2D? BakeMask(this GameObject objectToBake, SnowfallWeather snowfallData, int textureIndex = -1, int submeshIndex = 0)
        {
            Mesh mesh = objectToBake.GetComponent<MeshFilter>().sharedMesh;

            if (mesh == null)
            {
                Debug.LogError("No mesh found on object to bake!");
                return null;
            }

            Debug.LogDebug("Baking mask for " + objectToBake.name + " with texture index " + textureIndex + " and submesh index " + submeshIndex);

            snowfallData.RefreshBakeMaterial();

            RenderTexture tempRT = RenderTexture.GetTemporary(snowfallData.BakeResolution, snowfallData.BakeResolution, 0, RenderTextureFormat.ARGBFloat);
            tempRT.wrapMode = TextureWrapMode.Clamp;
            tempRT.filterMode = FilterMode.Trilinear;

            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = tempRT;
            GL.Clear(true, true, Color.clear);
            
            var matrix = objectToBake.transform.localToWorldMatrix;
            if (snowfallData.bakeMaterial?.SetPass(0) ?? false)
                Graphics.DrawMeshNow(mesh, matrix, submeshIndex);

            RenderTexture blurRT1 = RenderTexture.GetTemporary(snowfallData.BakeResolution, snowfallData.BakeResolution, 0, RenderTextureFormat.ARGBFloat);
            blurRT1.wrapMode = TextureWrapMode.Clamp;
            blurRT1.filterMode = FilterMode.Trilinear;

            RenderTexture blurRT2 = RenderTexture.GetTemporary(snowfallData.BakeResolution, snowfallData.BakeResolution, 0, RenderTextureFormat.ARGBFloat);
            blurRT2.wrapMode = TextureWrapMode.Clamp;
            blurRT2.filterMode = FilterMode.Trilinear;

            // Blur the normal map horizontally
            Graphics.Blit(tempRT, blurRT1, snowfallData.bakeMaterial, 1);
            // Blur the normal map vertically
            Graphics.Blit(blurRT1, blurRT2, snowfallData.bakeMaterial, 2);

            RenderTexture.active = blurRT2;
            Texture2D maskTexture = new Texture2D(snowfallData.BakeResolution, snowfallData.BakeResolution, TextureFormat.RGBAFloat, false);
            maskTexture.wrapMode = TextureWrapMode.Clamp;
            maskTexture.filterMode = FilterMode.Trilinear;
            maskTexture.ReadPixels(new Rect(0, 0, snowfallData.BakeResolution, snowfallData.BakeResolution), 0, 0);

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
                Graphics.CopyTexture(maskTexture, 0, 0, snowfallData.snowMasks, textureIndex, 0);
                Object.DestroyImmediate(maskTexture);
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

        /// <summary>
        ///  Converts a Terrain object to a mesh terrain with the specified SnowfallData parameters.
        ///  </summary>
        ///  <param name="terrain">The Terrain object to convert.</param>
        ///  <param name="snowfallData">The SnowfallData object containing the mesh generation parameters.</param>
        ///  <param name="levelBounds">The bounds within which the mesh should be generated, in world space.</param>
        ///  <returns>The GameObject containing the mesh terrain.</returns>
        ///  <remarks>
        ///  This method creates a new GameObject with a MeshFilter and MeshRenderer component, and a MeshCollider if specified.
        ///  The mesh terrain is generated based on the heightmap of the Terrain object.
        ///  The mesh density is determined by the SnowfallData parameters, with the levelBounds used to refine the mesh within the specified area.
        ///  </remarks>
        ///  <exception cref="System.ArgumentNullException">Thrown if `terrain` or `snowfallData` is null.</exception>
        internal static GameObject Meshify(this Terrain terrain, SnowfallWeather snowfallData, Bounds? levelBounds)
        {
            GameObject meshTerrain = new GameObject("MeshTerrain_" + terrain.name);
            MeshFilter meshFilter = meshTerrain.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshTerrain.AddComponent<MeshRenderer>();
            // Set same rendering layer, rendering layer mask and tag as the terrain (and set the snow overlay custom pass layer)
            meshRenderer.gameObject.layer = terrain.gameObject.layer;
            meshRenderer.gameObject.tag = terrain.gameObject.tag;
            terrain.renderingLayerMask |= (uint)(snowfallData.snowOverlayCustomPass?.renderingLayers ?? 0);
            meshRenderer.renderingLayerMask = terrain.renderingLayerMask;
            meshTerrain.isStatic = true;

            Mesh mesh = new Mesh();
            mesh.name = "MeshTerrain_" + terrain.name;

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            meshTerrain.transform.position = terrain.transform.position;

            float terrainWidth = terrain.terrainData.size.x;
            float terrainLength = terrain.terrainData.size.z;
            float terrainHeight = terrain.terrainData.size.y;

            float terrainStepX = terrainWidth / (terrain.terrainData.heightmapResolution - 1);
            float terrainStepZ = terrainLength / (terrain.terrainData.heightmapResolution - 1);
            
            float uvStepX = 1.0f / (terrain.terrainData.heightmapResolution - 1);
            float uvStepZ = 1.0f / (terrain.terrainData.heightmapResolution - 1);
            
            int actualCellStep;

            HashSet<Vector3> holeVertices = new HashSet<Vector3>();

            if (levelBounds != null) // Use the level bounds to determine the mesh density
            {   
                Bounds terrainBounds = terrain.terrainData.bounds;
                terrainBounds.center += terrain.transform.position;
                float terrainSize = Mathf.Max(terrainBounds.extents.x, terrainBounds.extents.z);
                //Debug.LogDebug"Terrain center: " + terrainBounds.center + " Terrain Size: " + terrainSize);

                Vector3 levelCenter = levelBounds.Value.center;
                float levelSize = Mathf.Max(levelBounds.Value.extents.x, levelBounds.Value.extents.z);
                //Debug.LogDebug"Level Center: " + levelCenter + " Level Size: " + levelSize);

                int minMeshStep = snowfallData.MinMeshStep;
                if (snowfallData.TargetVertexCount > 0)
                {
                    minMeshStep = Mathf.CeilToInt(Mathf.Sqrt(levelSize * levelSize / (terrainStepX * terrainStepZ * snowfallData.TargetVertexCount)));
                }

                //Debug.LogDebug"Base Density Factor: " + minMeshStep);

                QuadTree rootNode = new QuadTree(terrainBounds);
                rootNode.Subdivide(levelBounds.Value, new Vector2(terrainStepX, terrainStepZ), minMeshStep,
                                    snowfallData.MaxMeshStep, snowfallData.FalloffSpeed, terrainSize - levelSize);

                // Generate vertices from 4 corners of leaf nodes of the quadtree
                HashSet<Vector3> uniqueVertices = new HashSet<Vector3>();
                List<QuadTree> leafNodes = new List<QuadTree>();
                rootNode.GetLeafNodes(leafNodes);

                foreach (var node in leafNodes)
                {
                    Vector3[] corners = new Vector3[]
                    {
                        new Vector3(node.bounds.min.x, 0, node.bounds.min.z),
                        new Vector3(node.bounds.min.x, 0, node.bounds.max.z),
                        new Vector3(node.bounds.max.x, 0, node.bounds.min.z),
                        new Vector3(node.bounds.max.x, 0, node.bounds.max.z)
                    };

                    foreach (var corner in corners)
                    {
                        if (uniqueVertices.Add(corner))
                        {
                            float height = terrain.SampleHeight(corner);
                            Vector3 vertex = new Vector3(corner.x - terrain.transform.position.x, height, corner.z - terrain.transform.position.z);
                            vertices.Add(vertex);

                            Vector2 uv = new Vector2(vertex.x / terrainWidth, vertex.z / terrainLength);
                            uvs.Add(uv);

                            if (snowfallData.CarveHoles)
                            {
                                int heightmapX = (int)((vertex.x) / terrainStepX );
                                int heightmapZ = (int)((vertex.z) / terrainStepZ);
                                heightmapX = Mathf.Clamp(heightmapX, 0, terrain.terrainData.holesResolution - 1);
                                heightmapZ = Mathf.Clamp(heightmapZ, 0, terrain.terrainData.holesResolution - 1);

                                // Check if the vertex is inside a terrain hole
                                if (!terrain.terrainData.GetHoles(heightmapX, heightmapZ, 1, 1)[0, 0])
                                {
                                    holeVertices.Add(vertex);
                                }
                            }
                        }
                    }
                }

                Debug.LogDebug("Sampled vertices: " + vertices.Count);

                var polygon = new Polygon();

                for (int i = 0; i < vertices.Count; i++)
                {
                    Vertex triNetVertex = vertices[i].ToTriangleNetVertex(uvs[i], 1);
                    polygon.Add(triNetVertex);
                }

                // Configure triangulation options
                ConstraintOptions options = new ConstraintOptions() { ConformingDelaunay = false, SegmentSplitting = 2};
                QualityOptions quality = new QualityOptions() { MinimumAngle = 20.0f, SteinerPoints = snowfallData.RefineMesh ? -1 : 0};

                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                // Perform triangulation
                sw.Restart();
                var mesh2d = polygon.Triangulate(options, quality);
                sw.Stop();
                Debug.LogDebug("Triangulation time: " + sw.ElapsedMilliseconds + "ms");
                Debug.LogDebug("Final vertices: " + mesh2d.Vertices.Count);

                // Convert the 2D mesh to Unity mesh
                vertices = new List<Vector3>();
                Dictionary<int, int> vertexIDs = new Dictionary<int, int>();
                uvs = new List<Vector2>();

                foreach (ITriangle triangle in mesh2d.Triangles)
                {
                    int v0 = triangle.GetVertexID(0);
                    int v1 = triangle.GetVertexID(1);
                    int v2 = triangle.GetVertexID(2);

                    if (!vertexIDs.ContainsKey(v0))
                    {
                        vertexIDs[v0] = vertices.Count;
                        vertices.Add(triangle.GetVertex(0).ToVector3(1));
                        uvs.Add(triangle.GetVertex(0).UV);
                    }
                    if (!vertexIDs.ContainsKey(v1))
                    {
                        vertexIDs[v1] = vertices.Count;
                        vertices.Add(triangle.GetVertex(1).ToVector3(1));
                        uvs.Add(triangle.GetVertex(1).UV);
                    }
                    if (!vertexIDs.ContainsKey(v2))
                    {
                        vertexIDs[v2] = vertices.Count;
                        vertices.Add(triangle.GetVertex(2).ToVector3(1));
                        uvs.Add(triangle.GetVertex(2).UV);
                    }

                    if (snowfallData.CarveHoles)
                    {
                        if (holeVertices.Contains(vertices[vertexIDs[v0]]) ||
                            holeVertices.Contains(vertices[vertexIDs[v1]]) ||
                            holeVertices.Contains(vertices[vertexIDs[v2]]))
                        {
                            continue;
                        }
                    }

                    triangles.Add(vertexIDs[v0]);
                    triangles.Add(vertexIDs[v2]);
                    triangles.Add(vertexIDs[v1]);
                }
            }
            else // Uniform meshing if no level bounds are set
            {
                int minMeshStep = snowfallData.MinMeshStep;
                // Calculate density factor to achieve target vertex count
                if (snowfallData.TargetVertexCount > 0)
                {
                    minMeshStep = Mathf.CeilToInt(Mathf.Sqrt(terrain.terrainData.heightmapResolution * terrain.terrainData.heightmapResolution / snowfallData.TargetVertexCount));
                }

                actualCellStep = Mathf.Max(minMeshStep, 1);
                //Debug.LogDebug"Density Factor: " + actualCellStep);

                // Calculate grid dimensions after applying density factor
                int gridWidth = Mathf.FloorToInt(terrain.terrainData.heightmapResolution / actualCellStep);
                int gridHeight = Mathf.FloorToInt(terrain.terrainData.heightmapResolution / actualCellStep);

                // Generate vertices
                for (int z = 0; z <= gridHeight; z++)
                {
                    for (int x = 0; x <= gridWidth; x++)
                    {
                        // Convert grid coordinates back to heightmap coordinates
                        int heightmapX = x * actualCellStep;
                        int heightmapZ = z * actualCellStep;

                        // Clamp to prevent accessing outside heightmap bounds
                        heightmapX = Mathf.Min(heightmapX, terrain.terrainData.heightmapResolution - 1);
                        heightmapZ = Mathf.Min(heightmapZ, terrain.terrainData.heightmapResolution - 1);

                        float height = terrain.terrainData.GetHeight(heightmapX, heightmapZ);
                        Vector3 vertex = new Vector3(heightmapX * terrainStepX, height, heightmapZ * terrainStepZ);
                        vertices.Add(vertex);

                        Vector2 uv = new Vector2(heightmapX * uvStepX, heightmapZ * uvStepZ);
                        uvs.Add(uv);

                        if (snowfallData.CarveHoles)
                        {
                            heightmapX = Mathf.Clamp(heightmapX, 0, terrain.terrainData.holesResolution - 1);
                            heightmapZ = Mathf.Clamp(heightmapZ, 0, terrain.terrainData.holesResolution - 1);

                            // Check if the vertex is inside a terrain hole
                            if (!terrain.terrainData.GetHoles(heightmapX, heightmapZ, 1, 1)[0, 0])
                            {
                                holeVertices.Add(vertex);
                            }
                        }
                    }
                }

                Debug.LogDebug("Sampled vertices: " + vertices.Count);

                // Generate triangles using grid coordinates
                for (int z = 0; z < gridHeight; z++)
                {
                    for (int x = 0; x < gridWidth; x++)
                    {
                        // Calculate vertex indices in the grid
                        int vertexIndex = z * (gridWidth + 1) + x;

                        if (snowfallData.CarveHoles)
                        {
                            if (holeVertices.Contains(vertices[vertexIndex]) ||
                                holeVertices.Contains(vertices[vertexIndex + 1]) ||
                                holeVertices.Contains(vertices[vertexIndex + (gridWidth + 1)]) ||
                                holeVertices.Contains(vertices[vertexIndex + (gridWidth + 1) + 1]))
                            {
                                continue;
                            }
                        }

                        // First triangle
                        triangles.Add(vertexIndex);                     // Current vertex
                        triangles.Add(vertexIndex + (gridWidth + 1));   // Vertex below
                        triangles.Add(vertexIndex + (gridWidth + 1) + 1); // Vertex below and right

                        // Second triangle
                        triangles.Add(vertexIndex);                     // Current vertex
                        triangles.Add(vertexIndex + (gridWidth + 1) + 1); // Vertex below and right
                        triangles.Add(vertexIndex + 1);                 // Vertex to the right
                    }
                }
            }

            mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.Optimize(); // Clean unused vertices
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            if (snowfallData.UseMeshCollider)
            {
                MeshCollider meshCollider = meshTerrain.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
                // Disable terrain collider
                terrain.GetComponent<TerrainCollider>().enabled = false;
            }

            meshFilter.mesh = mesh;
            meshRenderer.sharedMaterial = new Material(snowfallData.terraMeshShader);
            // Disable rendering of terrain
            terrain.drawHeightmap = false;
            // terrain.drawInstanced = false; // TODO: Check if this is necessary

            //We have to copy the trees if we want to use the mesh collider, no way to disable only the tree colliders at runtime
            if (snowfallData.copyTrees || snowfallData.UseMeshCollider) 
            {
                //Disable rendering of trees on terrain
                terrain.treeDistance = 0;
                // Create Trees parent object
                Transform treesParent = new GameObject("Trees").transform;
                treesParent.position = meshTerrain.transform.position;
                treesParent.parent = meshTerrain.transform;

                // Copy trees to the mesh terrain
                foreach (TreeInstance tree in terrain.terrainData.treeInstances)
                {
                    // Get tree prototype
                    GameObject treePrototype = terrain.terrainData.treePrototypes[tree.prototypeIndex].prefab;

                    // Instantiate tree
                    GameObject newTree = GameObject.Instantiate(treePrototype, treesParent);

                    // Calculate tree position
                    Vector3 treeWorldPos = Vector3.Scale(tree.position, terrain.terrainData.size) + terrain.transform.position;

                    // Set tree transform
                    newTree.transform.position = treeWorldPos;
                    newTree.transform.localScale = new Vector3(tree.widthScale, tree.heightScale, tree.widthScale); // Assuming uniform width and length scale
                    newTree.transform.rotation = Quaternion.Euler(0, tree.rotation * 180f/Mathf.PI, 0); // Convert rotation to degrees
                
                    // Set to static
                    newTree.isStatic = true;

                    // Set rendering layers
                    newTree.layer = LayerMask.GetMask("Terrain");
                    if (newTree.TryGetComponent<MeshRenderer>(out MeshRenderer renderer))
                    {
                        renderer.renderingLayerMask |= (uint)(snowfallData.snowOverlayCustomPass?.renderingLayers ?? 0);
                    }
                }
            }

            if (snowfallData.copyDetail)
            {
                Transform grassParent = new GameObject("Grass").transform;
                grassParent.parent = meshTerrain.transform;

                var terrainData = terrain.terrainData;
                var scaleX = terrainData.size.x / terrainData.detailWidth;
                var scaleZ = terrainData.size.z / terrainData.detailHeight;
                Debug.LogDebug("Detail Prototypes: " + terrainData.detailPrototypes.Length);
                
                
                for (int d = 0; d < terrainData.detailPrototypes.Length; d++)
                {
                    var detailPrototype = terrainData.detailPrototypes[d];
                    var detailLayer = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, d);
                    float targetDensity = detailPrototype.density;
                    Debug.LogDebug("Target Coverage: " + detailPrototype.targetCoverage);
                    for (int x = 0; x < terrainData.detailWidth; x++)
                    {
                        for (int y = 0; y < terrainData.detailHeight; y++)
                        {
                            var layerDensity = detailLayer[y, x]/255f;
                            float posX = x * scaleX + terrain.transform.position.x;
                            float posZ = y * scaleZ + terrain.transform.position.z;
                            float perlinNoise = Mathf.PerlinNoise(posX, posZ);
                            if (perlinNoise * layerDensity * targetDensity > 0.9f)
                            {
                                //Debug.Log("Density factor: " + perlinNoise * layerDensity * targetDensity);
                                var pos = new Vector3(posX, 0, posZ);
                                pos.y = terrain.SampleHeight(pos);
                                var detail = GameObject.Instantiate(terrainData.detailPrototypes[d].prototype, pos, Quaternion.Euler(0, UnityEngine.Random.Range(0, 359), 0), grassParent);

                                var scale = UnityEngine.Random.Range(detailPrototype.minWidth, detailPrototype.maxWidth);
                                var height = UnityEngine.Random.Range(detailPrototype.minHeight, detailPrototype.maxHeight);
                                detail.transform.localScale = new Vector3(scale, height, scale);
                            }

                        }
                    }
                }
            }

            return meshTerrain;
        }

        public static Texture2D[] GetSplatmapsAsTextures(this Terrain terrain)
        {

            TerrainData terrainData = terrain.terrainData;
            int alphamapWidth = terrainData.alphamapWidth;
            int alphamapHeight = terrainData.alphamapHeight;
            float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
            int numSplatmaps = terrainData.alphamapLayers;
            
            // Calculate how many RGBA textures we need
            int numTextures = Mathf.CeilToInt(numSplatmaps / 4f);
            Texture2D[] splatmaps = new Texture2D[numTextures];

            for (int textureIndex = 0; textureIndex < numTextures; textureIndex++)
            {
                Texture2D packedTexture = new Texture2D(alphamapWidth, alphamapHeight, TextureFormat.RGBA32, false);
                Color[] packedColors = new Color[alphamapWidth * alphamapHeight];

                for (int y = 0; y < alphamapHeight; y++)
                {
                    for (int x = 0; x < alphamapWidth; x++)
                    {
                        float r = textureIndex * 4 + 0 < numSplatmaps ? splatmapData[y, x, textureIndex * 4 + 0] : 0f;
                        float g = textureIndex * 4 + 1 < numSplatmaps ? splatmapData[y, x, textureIndex * 4 + 1] : 0f;
                        float b = textureIndex * 4 + 2 < numSplatmaps ? splatmapData[y, x, textureIndex * 4 + 2] : 0f;
                        float a = textureIndex * 4 + 3 < numSplatmaps ? splatmapData[y, x, textureIndex * 4 + 3] : 0f;

                        packedColors[y * alphamapWidth + x] = new Color(r, g, b, a);
                    }
                }

                packedTexture.SetPixels(packedColors);
                packedTexture.Apply();

                splatmaps[textureIndex] = packedTexture;

                Debug.LogDebug($"Saved splatmap_{textureIndex}.png with layers:" +
                    $"\nR: Layer {textureIndex * 4 + 0}" +
                    (textureIndex * 4 + 1 < numSplatmaps ? $"\nG: Layer {textureIndex * 4 + 1}" : "") +
                    (textureIndex * 4 + 2 < numSplatmaps ? $"\nB: Layer {textureIndex * 4 + 2}" : "") +
                    (textureIndex * 4 + 3 < numSplatmaps ? $"\nA: Layer {textureIndex * 4 + 3}" : ""));
            }

            return splatmaps;
        }

        public static void SetupMaterialFromTerrain(this Material targetMaterial, Terrain terrain)
        {
            TerrainLayer[] terrainLayers = terrain.terrainData.terrainLayers;
            int layerCount = terrainLayers.Length;

            if (layerCount > 8)
            {
                Debug.LogWarning("Terrain has more than 8 layers. Only the first 8 will be used.");
                layerCount = 8;
            }

            // Get splatmaps
            Texture2D[] splatmaps = terrain.GetSplatmapsAsTextures();

            for (int i = 0; i < splatmaps.Length; i++)
            {
                targetMaterial.SetTexture($"_Splatmap_{i}", splatmaps[i]);
            }

            // Process each layer
            for (int i = 0; i < layerCount; i++)
            {
                TerrainLayer layer = terrainLayers[i];

                //Skip empty layers
                if (layer.diffuseTexture == null)
                {
                    continue;
                }
                
                // Set textures
                targetMaterial.SetTexture($"_Albedo_{i}", layer.diffuseTexture);
                targetMaterial.SetTexture($"_Normals_{i}", layer.normalMapTexture);
                targetMaterial.SetTexture($"_Mask_{i}", layer.maskMapTexture);

                targetMaterial.SetColor($"_Color_Tint_{i}", layer.diffuseRemapMax);
                targetMaterial.SetFloat($"_Normal_Scale_{i}", layer.normalScale);

                // Set tiling and offset
                targetMaterial.SetVector($"_Tiling_{i}", new Vector2(layer.tileSize.x, layer.tileSize.y));
                targetMaterial.SetVector($"_Offset_{i}", new Vector2(layer.tileOffset.x, layer.tileOffset.y));

                // Set remapping values
                if (layer.maskMapTexture == null)
                {
                    targetMaterial.SetVector($"_Metallic_Remapping_{i}", new Vector2(layer.metallic, layer.metallic));
                    targetMaterial.SetVector($"_AO_Remapping_{i}", new Vector2(1, 1));
                    if (GraphicsFormatUtility.GetAlphaComponentCount(layer.diffuseTexture.format) > 0)
                    {
                        targetMaterial.SetVector($"_Smoothness_Remapping_{i}", new Vector2(0, 1));
                    }
                    else
                    {
                        targetMaterial.SetVector($"_Smoothness_Remapping_{i}", new Vector2(layer.smoothness, layer.smoothness));
                    }
                }
                else
                {
                    targetMaterial.SetVector($"_Metallic_Remapping_{i}", new Vector2(layer.maskMapRemapMin.x, layer.maskMapRemapMax.x));
                    targetMaterial.SetVector($"_AO_Remapping_{i}", new Vector2(layer.maskMapRemapMin.y, layer.maskMapRemapMax.y));
                    targetMaterial.SetVector($"_Smoothness_Remapping_{i}", new Vector2(layer.maskMapRemapMin.w, layer.maskMapRemapMax.w));
                }
            }

            // Clear unused layers
            for (int i = layerCount; i < 8; i++)
            {
                Texture2D emptyTex = Texture2D.blackTexture;

                targetMaterial.SetTexture($"_Albedo_{i}", emptyTex);
                targetMaterial.SetTexture($"_Normals_{i}", emptyTex);
                targetMaterial.SetTexture($"_Mask_{i}", emptyTex);
                targetMaterial.SetColor($"_Color_Tint_{i}", Vector4.zero);
                targetMaterial.SetFloat($"_Normal_Scale_{i}", 0f);
                targetMaterial.SetVector($"_Tiling_{i}", Vector2.one);
                targetMaterial.SetVector($"_Offset_{i}", Vector2.zero);
                targetMaterial.SetVector($"_Metallic_Remapping_{i}", new Vector2(0, 1));
                targetMaterial.SetVector($"_AO_Remapping_{i}", new Vector2(0, 1));
                targetMaterial.SetVector($"_Smoothness_Remapping_{i}", new Vector2(0, 1));
            }
        }
    
        public static GameObject Duplicate(this GameObject original, bool disableShadows = true, bool removeCollider = true)
        {
            GameObject duplicate = GameObject.Instantiate(original);
            duplicate.name = original.name + "_Copy";
            // Copy transform and hierarchy
            duplicate.transform.SetParent(original.transform.parent);
            duplicate.transform.localPosition = original.transform.localPosition;
            duplicate.transform.localRotation = original.transform.localRotation;
            duplicate.transform.localScale = original.transform.localScale;
            if (disableShadows)
            {
                MeshRenderer[] renderers = duplicate.GetComponentsInChildren<MeshRenderer>();
                foreach (MeshRenderer renderer in renderers)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            if (removeCollider)
            {
                Collider[] colliders = duplicate.GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders)
                {
                    GameObject.DestroyImmediate(collider);
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
    
    }
}
