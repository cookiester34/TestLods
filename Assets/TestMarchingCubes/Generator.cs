using System;
using System.Collections.Generic;
using System.Linq;
using TerrainBakery.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Generator : MonoBehaviour
{
    [SerializeField] private Material Material;
    private Dictionary<Vector3, chunkData> chunks = new();

    private void OnDrawGizmos()
    {
        var chunkSize = 32f / 2;
        foreach (var (position, chunk) in chunks)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(position + new Vector3(chunkSize, chunkSize, chunkSize), new Vector3(33, 33, 33));
            // Gizmos.color = Color.magenta;
            // Gizmos.DrawWireMesh(chunk.Mesh, position, quaternion.identity, new Vector3(1,1,1));
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        var chunkSize = 32;
        var chunkSizeDoubled = chunkSize * 2;
        var terrainMapSize = chunkSizeDoubled * chunkSizeDoubled * chunkSize;
        
        for (int l = -2; l < 2; l++)
        for (int i = -2; i < 2; i++)
        for (int j = -2; j < 2; j++)

        {
            var chunkPosition = new Vector3(i * chunkSize, l * chunkSize, j * chunkSize);
            var terrainMapJob = new TerrainMapJob
            {
                chunkSize = chunkSize + 1,
                chunkPosition = chunkPosition,
                planetSize = 50,
                octaves = 1,
                weightedStrength = 1,
                lacunarity = 1,
                gain = 1,
                octavesCaves = 1,
                weightedStrengthCaves = 1,
                lacunarityCaves = 1,
                gainCaves = 1,
                domainWarpAmp = 1,
                terrainMap = new NativeArray<float>(terrainMapSize, Allocator.TempJob),
                seed = 1
            };
            terrainMapJob.Schedule(chunkSize + 1, 244).Complete();

            var meshDataJob = new MeshDataJob
            {
                chunkSize = chunkSize + 1,
                terrainMap = new NativeArray<float>(terrainMapJob.terrainMap, Allocator.TempJob),
                terrainSurface = 0.5f,
                cube = new NativeArray<float>(8, Allocator.TempJob),
                smoothTerrain = true,
                flatShaded = false,
                triCount = new NativeArray<int>(1, Allocator.TempJob),
                vertCount = new NativeArray<int>(1, Allocator.TempJob),
                //max number of vertices: 65535
                vertices = new NativeArray<Vector3>(65535, Allocator.TempJob),
                triangles = new NativeArray<int>(65535, Allocator.TempJob),
                lodIndex = 0
            };

            meshDataJob.Schedule().Complete();

            var triCount = meshDataJob.triCount.ToArray();
            var vertCount = meshDataJob.vertCount.ToArray();
            var lodIndex = meshDataJob.lodIndex;

            var MeshDataSet = new chunkData();
            var tCount = triCount[0];
            MeshDataSet.chunkTriangles = new int[tCount];
            for (var k = 0; k < tCount; k++) MeshDataSet.chunkTriangles[k] = meshDataJob.triangles[k];
            var vCount = vertCount[0];
            MeshDataSet.chunkVertices = new Vector3[vCount];
            for (var k = 0; k < vCount; k++) MeshDataSet.chunkVertices[k] = meshDataJob.vertices[k];
            var meshFilterMesh = new Mesh
            {
                vertices = MeshDataSet.chunkVertices,
                triangles = MeshDataSet.chunkTriangles,
            };

            // if (i != 1)
            // {
                meshFilterMesh = GenerateLowerLODMesh(meshFilterMesh.triangles, meshFilterMesh.vertices, chunkSize);
            // }
            
            
            meshFilterMesh.RecalculateNormals();
            MeshDataSet.Mesh = meshFilterMesh;
            chunks.Add(chunkPosition, MeshDataSet);


            var gameobject = new GameObject(i == 1 ? "LOD: 0" : "LOD: 3");
            gameobject.transform.position = chunkPosition;
            var meshFilter = gameobject.AddComponent<MeshFilter>();
            meshFilter.mesh = meshFilterMesh;
            var meshRenderer = gameobject.AddComponent<MeshRenderer>();
            meshRenderer.material = Material;

            meshDataJob.cube.Dispose();
            meshDataJob.terrainMap.Dispose();
            meshDataJob.triCount.Dispose();
            meshDataJob.vertCount.Dispose();
            meshDataJob.vertices.Dispose();
            meshDataJob.triangles.Dispose();
            terrainMapJob.terrainMap.Dispose();
        }
    }
    
    public class chunkData
    {
        public int[] chunkTriangles;
        public Vector3[] chunkVertices;
        public Mesh Mesh;
    }



    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// START OF BENAS' CODE
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    private static Dictionary<int, List<int>> GetConnectedIndices(IReadOnlyList<int> o_triangle_indices, IReadOnlyCollection<Vector3> o_points)
    {
        Dictionary<int, List<int>> connected_indices = new();

        for (int i = 0; i < o_points.Count; i++)
        {
            connected_indices[i] = new List<int>();

            for (int j = 0; j < o_triangle_indices.Count; j++)
            {
                if (o_triangle_indices[j] == i)
                {
                    int t_start = j - (j % 3);

                    for (int k = t_start; k < t_start + 3; k++)
                    {
                        if (o_triangle_indices[k] != i)
                        {
                            connected_indices[i].Add(o_triangle_indices[k]);
                        }
                    }
                }
            }

            connected_indices[i] = connected_indices[i].Distinct().ToList();
        }

        return connected_indices;
    }

    private static Dictionary<int, Vector3> GetAverageNormalForRemovedTriangles(IReadOnlyList<int> o_triangle_indices, IReadOnlyList<Vector3> o_points)
    {
        Dictionary<int, Vector3> reference_triangle = new();
        for (int i = 0; i < o_points.Count; i++)
        {
            // reference_triangle[i] = new Vector3();
            // continue;
            List<Vector3> normals = new();
            for (int j = 0; j < o_triangle_indices.Count; j++)
            {
                int t_start = j - (j % 3);

                if (o_triangle_indices[t_start] == i ||
                     o_triangle_indices[t_start + 1] == i ||
                     o_triangle_indices[t_start + 2] == i)
                {
                    var a = o_points[o_triangle_indices[t_start + 1]] - o_points[o_triangle_indices[t_start]];
                    var b = o_points[o_triangle_indices[t_start + 2]] - o_points[o_triangle_indices[t_start]];

                    float nx = a.y * b.z - a.z * b.y;
                    float ny = a.z * b.x - a.x * b.z;
                    float nz = a.x * b.y - a.y * b.x;

                    normals.Add(Vector3.Normalize((new Vector3(nx, ny, nz))));

                }
            }
            reference_triangle[i] = new Vector3();
            foreach (var normal in normals)
            {
                reference_triangle[i] += normal;
            }

            reference_triangle[i] /= normals.Count;
        }
        return reference_triangle;
    }

    private static List<int> GetRemovedPoints(IList<Vector3> o_points, float chunk_size, IReadOnlyDictionary<int, List<int>> connected_indices)
    {
        List<int> removed_indices_to_original = new();
        for (int index = 0; index < o_points.Count; index++)
        {
            float x = o_points[index].x;
            float y = o_points[index].y;
            float z = o_points[index].z;

            // IF EDGE LEAVE ALONE
            if (x <= 0 || x >= chunk_size
                       ||
               y <= 0 || y >= chunk_size
               ||
               z <= 0 || z >= chunk_size)
            {
                continue;
            }

            // CHECK EACH CONNECTION TO SEE IF IT IS CONNECTED TO A REMOVED POINT
            bool connected_to_removed = false;

            List<int> connected_points = connected_indices[index];

            foreach (int t in connected_points)
            {
                if (removed_indices_to_original.Contains(t))
                {
                    connected_to_removed = true;
                    break;
                }
            }

            // IF NOT CONNECTED TO REMOVED POINT, REMOVE, ELSE KEEP
            if (!connected_to_removed)
            {
                // kept_indices_to_original.Add(index);
                removed_indices_to_original.Add(index);
            }
        }
        return removed_indices_to_original;
    }

    private static List<Triangle> GenerateTrianglesForGaps(IReadOnlyList<Vector3> o_points, List<int> removed_indices_to_original, IReadOnlyDictionary<int, List<int>> connected_indices, IReadOnlyDictionary<int, Vector3> reference_triangle)
    {
        // This should not be this many nested statements. I'm sorry. needs some damn functions.
        List<Triangle> triangles = new();

        foreach (int removed_point in removed_indices_to_original)
        {
            List<int> connected_to_removed_points = connected_indices[removed_point];

            List<int> already_connected = new();
            int count = 0;
            while ((connected_to_removed_points.Count - already_connected.Count) > 2)
            {
                List<int> tried = new();
                foreach (int connected_to_removed in connected_to_removed_points)
                {
                    if (already_connected.Contains(connected_to_removed)) continue;
                    if (tried.Contains(connected_to_removed)) continue;

                    if ((connected_to_removed_points.Count - already_connected.Count) <= 2) break;
                    List<int> connected_to_connected_points = connected_indices[connected_to_removed];
                    List<int> triangle_points = new();
                    foreach (int connected_to_connected in connected_to_connected_points)
                    {
                        if (connected_to_removed_points.Contains(connected_to_connected) &&
                            !(already_connected.Contains(connected_to_connected)))
                        {
                            triangle_points.Add(connected_to_connected);
                            if (triangle_points.Count == 2)
                            {
                                // make sure no removed edges getting added. Shouldn't happen but just in case
                                if (removed_indices_to_original.Contains(connected_to_removed) ||
                                    removed_indices_to_original.Contains(triangle_points[0]) ||
                                    removed_indices_to_original.Contains(triangle_points[1])
                                   ) break;

                                // For working out normal of triangle to see which way to draw it.
                                var a = o_points[triangle_points[0]] - o_points[connected_to_removed];
                                var b = o_points[triangle_points[1]] - o_points[connected_to_removed];

                                float nx = a.y * b.z - a.z * b.y;
                                float ny = a.z * b.x - a.x * b.z;
                                float nz = a.x * b.y - a.y * b.x;

                                var n = math.normalize(new Vector3(nx, ny, nz));
                                var r = reference_triangle[removed_point];

                                float angle = Vector3.Angle(n, r);
                                float sign = Mathf.Sign(Vector3.Dot(new Vector3(0, 1, 0), Vector3.Cross(n, r)));

                                float signed_angle = angle * sign;

                                // If more than 90 away from average of all removed points then we assume we need to draw it the other way.
                                if (math.abs(signed_angle) < 90)
                                {
                                    triangles.Add(new Triangle(connected_to_removed, triangle_points[0], triangle_points[1]));
                                }
                                else
                                {
                                    triangles.Add(new Triangle(connected_to_removed, triangle_points[1], triangle_points[0]));
                                }

                                // Only add the edge if its not already there.
                                if (!connected_indices[triangle_points[0]].Contains(triangle_points[1]))
                                {
                                    connected_indices[triangle_points[0]].Add(triangle_points[1]);
                                }
                                if (!connected_indices[triangle_points[1]].Contains(triangle_points[0]))
                                {
                                    connected_indices[triangle_points[1]].Add(triangle_points[0]);
                                }

                                tried.Add(triangle_points[0]);
                                tried.Add(triangle_points[1]);

                                already_connected.Add(connected_to_removed);
                                break;
                            }
                        }
                    }
                }
                // this is weird... Seems to loop forever otherwise... Think this has something to do with why there's pockets.
                count++;
                if (count > 10) break;
            }
        }

        return triangles;
    }

    private static List<Triangle> GetUnchangedTriangles(IReadOnlyList<int> o_triangle_indices, ICollection<int> removed_indices_to_original)
    {
        List<Triangle> triangles = new();
        for (int index = 0; index < o_triangle_indices.Count; index += 3)
        {

            if (removed_indices_to_original.Contains(o_triangle_indices[index]) ||
                removed_indices_to_original.Contains(o_triangle_indices[index + 1]) ||
                removed_indices_to_original.Contains(o_triangle_indices[index + 2]))
            {
                // Do Nothing -> This had stuff before im not just a sick fuck that uses empty ifs...
            }
            else
            {
                triangles.Add(new Triangle(
                    o_triangle_indices[index],
                    o_triangle_indices[index + 1],
                    o_triangle_indices[index + 2]
                ));
            }
        }

        return triangles;
    }

    private Mesh Lod(IReadOnlyList<int> o_triangle_indices, Vector3[] o_points, int chunk_size)
    {
        // Used for calculating which way the triangles need to go.
        Dictionary<int, Vector3> reference_triangles = GetAverageNormalForRemovedTriangles(o_triangle_indices, o_points);

        // Dictionary of indices to all connected points from original.
        Dictionary<int, List<int>> connected_indices = GetConnectedIndices(o_triangle_indices, o_points);

        // Points which we want to remove
        List<int> removed_indices_to_original = GetRemovedPoints(o_points, chunk_size, connected_indices);

        List<Triangle> generated_triangles = GenerateTrianglesForGaps(o_points, removed_indices_to_original, connected_indices, reference_triangles);
        List<Triangle> unchanged_triangles = GetUnchangedTriangles(o_triangle_indices, removed_indices_to_original);

        generated_triangles.AddRange(unchanged_triangles);
        int[] indices = ConvertToIndexArray(generated_triangles);

        print("WENT FROM ::: " + (o_triangle_indices.Count / 3) + " ::: TRIANGLES TO ::: " + (generated_triangles.Count) + " ::: TRIANGLES");

        Mesh mesh = new()
        {
            vertices = o_points,
            triangles = indices
        };

        return mesh;
    }

    private Mesh GenerateLowerLODMesh(int[] o_triangle_indices, Vector3[] o_points, int chunk_size)
    {
        // CAN LOOP THIS FUNCTION TO MAKE IT CUT MORE.
        // BUT BECAUSE THEIRS POCKETS ITS MAKING ITERATIONS ON THE NEW MESH HAVE GIG MISSED TRIANGLES.
        var mesh = Lod(o_triangle_indices, o_points, chunk_size);
        
        return mesh;
    }
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// END OF BENAS' CODE
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////
    /// ///////////////////////////////

    private int[] ConvertToIndexArray(List<Triangle> triangles)
    {
        List<int> triangleArray = new List<int>();
        
        for (int i = 0; i < triangles.Count; i++)
        {
            triangleArray.Add(triangles[i].vertexIndexA);
            triangleArray.Add(triangles[i].vertexIndexB);
            triangleArray.Add(triangles[i].vertexIndexC);
        }
        
        return triangleArray.ToArray();
    }

    struct Triangle
    {
        public int vertexIndexA;
        public int vertexIndexB;
        public int vertexIndexC;
        
        public Triangle(int vertexIndexA, int vertexIndexB, int vertexIndexC)
        {
            this.vertexIndexA = vertexIndexA;
            this.vertexIndexB = vertexIndexB;
            this.vertexIndexC = vertexIndexC;
        }
    }

    // Find vertices on edges of mesh
    HashSet<int> FindEdgeVertices(Mesh mesh, Bounds chunkBounds) 
    {
        HashSet<int> edgeVerts = new HashSet<int>();
        // Loop through vertices
        for(int i = 0; i < mesh.vertices.Length; i++) 
        {
            // Get vertex position
            Vector3 vertPos = mesh.vertices[i];
            // Check if position is very close to chunk bounds
            if(Mathf.Approximately(vertPos.x, chunkBounds.min.x) || 
               Mathf.Approximately(vertPos.x, chunkBounds.max.x) ||
               Mathf.Approximately(vertPos.y, chunkBounds.min.y) ||
               Mathf.Approximately(vertPos.y, chunkBounds.max.y) ||
               Mathf.Approximately(vertPos.z, chunkBounds.min.z) ||
               Mathf.Approximately(vertPos.z, chunkBounds.max.z)) 
            {
                edgeVerts.Add(i);
            }
        }
        return edgeVerts;
    }
}
