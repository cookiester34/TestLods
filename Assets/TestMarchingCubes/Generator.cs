using System.Collections.Generic;
using System.Linq;
using TerrainBakery.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Generator : MonoBehaviour
{
    [SerializeField] private GameObject test;
    [SerializeField] private Material Material;
    private Dictionary<Vector3, chunkData> chunks = new();
    
    private List<Vector3> verticesLower = new List<Vector3>();
    private List<Vector3> verticesHigher = new List<Vector3>();

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

        for (var index = 0; index < verticesLower.Count; index++)
        {
            if (index >= 0 && index < verticesLower.Count)
            {
                var vector3 = verticesLower[index];
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(vector3, 1f);
            }
        }

        // for (var index = 0; index < verticesHigher.Count; index++)
        // {
        //     if (index >= 0 && index < verticesHigher.Count)
        //     {
        //         var vector3 = verticesHigher[index];
        //         Gizmos.color = Color.green;
        //         Gizmos.DrawSphere(vector3, 0.1f);
        //     }
        // }
    }

    // Start is called before the first frame update
    void Start()
    {
        var chunkSize = 32;
        var chunkSizeDoubled = chunkSize * 2;
        var terrainMapSize = chunkSizeDoubled * chunkSizeDoubled * chunkSize;
        
        // for (int l = -2; l < 2; l++)
        // for (int i = -2; i < 2; i++)
        for (int j = 1; j < 2; j++)

        {
            var chunkPosition = new Vector3(0 * chunkSize, 0 * chunkSize, j * chunkSize);
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

            var meshDataJob = new TransvoxelMeshDataJob
            {
                chunkSize = chunkSize + 1,
                terrainMap = new NativeArray<float>(terrainMapJob.terrainMap, Allocator.TempJob),
                terrainSurface = 0.5f,
                density = new NativeArray<int>(8, Allocator.TempJob),
                smoothTerrain = false,
                flatShaded = true,
                triCount = new NativeArray<int>(1, Allocator.TempJob),
                vertCount = new NativeArray<int>(1, Allocator.TempJob),
                //max number of vertices: 65535
                vertices = new NativeArray<Vector3>(90000, Allocator.TempJob),
                triangles = new NativeArray<int>(90000, Allocator.TempJob),
                lodIndex = 0,
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

            meshFilterMesh.RecalculateNormals();
            MeshDataSet.Mesh = meshFilterMesh;
            chunks.Add(chunkPosition, MeshDataSet);


            var gameobject = new GameObject(0 == 1 ? "LOD: 0" : "LOD: 3");
            gameobject.transform.position = chunkPosition;
            var meshFilter = gameobject.AddComponent<MeshFilter>();
            meshFilter.mesh = meshFilterMesh;
            var meshRenderer = gameobject.AddComponent<MeshRenderer>();
            meshRenderer.material = Material;

            meshDataJob.density.Dispose();
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
}
