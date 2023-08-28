using System;
using System.Collections.Generic;
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
                meshFilterMesh = GenerateLowerLODMesh(meshFilterMesh.triangles, meshFilterMesh.vertices, chunkSize, 2);
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

    Mesh GenerateLowerLODMesh(int[] originalIndices, Vector3[] originalVertices, int chunkSize, int reduceAmount)
    {
        List<Vector3> vertices = new(); //points
        List<Triangle> triangles = new(); //triangles
    
        int vertexIndex = 0;
    
        List<int> removedPoints = new List<int>();
        int reduce = 0;

        for (int index = 0; index < originalVertices.Length; index++)
        {
            var x = originalVertices[index].x;
            var y = originalVertices[index].z;
            
            if (x == 0 || x == chunkSize - 1f || y == 0 || y == chunkSize - 1f)
            {
                vertices.Add(originalVertices[index]);
                continue;
            }
    
            if (reduce == 0)
            {
                removedPoints.Add(index);
            }
            else
            {
                vertices.Add(originalVertices[index]);
            }
    
            reduce++;
            reduce %= reduceAmount; //2
        }
    
        List<uint> connected = new List<uint>();
    
        int removedPassed = 0;
        int removedAbove = 0;
        int removedBelow = 0;

        for (int index = 0; index < originalVertices.Length; index++)
        {
            bool removed = removedPoints.Contains(index);
    
            int left = index - 1 - removedPassed;
            int right = index - removedPassed;
            int top = index - chunkSize - removedAbove;
            int bottom = index + chunkSize - removedBelow;
    
            if (removed)
            {
                triangles.Add(new Triangle(top, right, left));
                triangles.Add(new Triangle(left, right, bottom));
    
                connected.Add((uint) (index - 1));
                connected.Add((uint) (index + 1));
                connected.Add((uint) (index - chunkSize));
                connected.Add((uint) (index + chunkSize));
    
                removedPassed++;
            }
    
            if (index - chunkSize >= 0)
            {
                bool topRemoved = removedPoints.Contains(index - chunkSize);
                if (topRemoved) removedAbove++;
            }
    
            if (index + chunkSize < originalVertices.Length)
            {
                bool bottomRemoved = removedPoints.Contains(index + chunkSize);
                if (bottomRemoved) removedBelow++;
            }
        }
    
        removedPassed = 0;
        removedAbove = 0;
        removedBelow = 0;
        
        for (int index = 0; index < originalVertices.Length; index++)
        {
            var x = originalVertices[index].x;
            var y = originalVertices[index].z;

            bool removed = removedPoints.Contains(index);

            // if Can FIND index then skip to next
            if (connected.Contains((uint) index) || removed)
            {
                if (removed)
                {
                    removedPassed++;
                }

                if (index - chunkSize >= 0)
                {
                    bool topRemoved = removedPoints.Contains(index - chunkSize);
                    if (topRemoved) removedAbove++;
                }

                if (index + chunkSize < originalVertices.Length)
                {
                    bool bottomRemoved = removedPoints.Contains(index + chunkSize);
                    if (bottomRemoved) removedBelow++;
                }

                continue;
            }

            int left = index - 1 - removedPassed;
            int right = index + 1 - removedPassed;
            int top = index - chunkSize - removedAbove;
            int bottom = index + chunkSize - removedBelow;

            if (y - 1 >= 0 && x - 1 >= 0)
            {
                triangles.Add(new Triangle(index - removedPassed, left, top));
            }

            if (x - 1 >= 0 && y + 1 < chunkSize)
            {
                triangles.Add(new Triangle(index - removedPassed, bottom, left));
            }

            if (y + 1 < chunkSize && x + 1 < chunkSize)
            {
                triangles.Add(new Triangle(index - removedPassed, right, bottom));
            }

            if (x + 1 < chunkSize && y - 1 >= 0)
            {
                triangles.Add(new Triangle(index - removedPassed, top, right));
            }

            if (index - chunkSize >= 0)
            {
                bool topRemoved = removedPoints.Contains(index - chunkSize);
                if (topRemoved) removedAbove++;
            }

            if (index + chunkSize < originalVertices.Length)
            {
                bool bottomRemoved = removedPoints.Contains(index + chunkSize);
                if (bottomRemoved) removedBelow++;
            }
        }

        for (int i = triangles.Count - 1; i >= 0; i--)
        {
            if (triangles[i].vertexIndexA < 0 || 
                triangles[i].vertexIndexB < 0 || 
                triangles[i].vertexIndexC < 0 ||
                triangles[i].vertexIndexA >= vertices.Count || 
                triangles[i].vertexIndexB >= vertices.Count || 
                triangles[i].vertexIndexC >= vertices.Count)
            {
                triangles.RemoveAt(i);
            }
        }

        var lodMesh = new Mesh();
        lodMesh.vertices = vertices.ToArray();
        lodMesh.triangles = ConvertToIndexArray(triangles);
    
        return lodMesh;
    }

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
