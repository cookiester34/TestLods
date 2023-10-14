using System.Linq;
using TerrainBakery.Jobs;
using TestMarchingCubes;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class Generator : MonoBehaviour
{
    [SerializeField] private GameObject test;
    [SerializeField] private Material Material;

    // Start is called before the first frame update
    void Start()
    {
        var chunkSize = 32;
        var chunkSizeDoubled = chunkSize * 2;
        var terrainMapSize = chunkSizeDoubled * chunkSizeDoubled * chunkSize;

        var chunkPosition = new Vector3(0, 0, 0);
        var terrainMapJob = new TerrainMapJob
        {
            chunkSize = chunkSize,
            chunkPosition = chunkPosition,
            planetSize = 30,
            octaves = 1,
            weightedStrength = 1,
            lacunarity = 0.2f,
            gain = 0.3f,
            octavesCaves = 1,
            weightedStrengthCaves = 0.2f,
            lacunarityCaves = 0.2f,
            gainCaves = 1,
            domainWarpAmp = 1,
            terrainMap = new NativeArray<float>(terrainMapSize, Allocator.TempJob),
            seed = 1
        };
        terrainMapJob.Schedule(chunkSize, 244).Complete();

        var meshDataJob = new TransvoxelMeshDataJob
        {
            chunkSize = chunkSize,
            lod = 1,
            terrainMap = new NativeArray<float>(terrainMapJob.terrainMap, Allocator.TempJob),
            triangles = new NativeArray<Triangle>(90000, Allocator.TempJob),
            triangleCount = new NativeArray<int>(1, Allocator.TempJob)
        };

        meshDataJob.Schedule().Complete();
        
        var MeshDataSet = new chunkData();
        MeshDataSet.chunkVertices = new Vector3[0];
        MeshDataSet.chunkTriangles = new int[0];
        
        var triCount = meshDataJob.triangleCount[0];
        var triangles = meshDataJob.triangles.ToArray();
        for (int i = 0; i < triCount; i++)
        {
            var triangle = triangles[i];
            MeshDataSet.chunkVertices = MeshDataSet.chunkVertices.Concat(new[]
                {
                    triangle.vertex1,
                    triangle.vertex2,
                    triangle.vertex3
                })
                .ToArray();
            MeshDataSet.chunkTriangles = MeshDataSet.chunkTriangles.Concat(new[]
                {
                    i * 3,
                    i * 3 + 1,
                    i * 3 + 2
                })
                .ToArray();
        }
        
        var meshFilterMesh = new Mesh
        {
            vertices = MeshDataSet.chunkVertices,
            triangles = MeshDataSet.chunkTriangles,
        };

        meshFilterMesh.RecalculateNormals();
        MeshDataSet.Mesh = meshFilterMesh;

        var gameobject = new GameObject(0 == 1 ? "LOD: 0" : "LOD: 3");
        gameobject.transform.position = chunkPosition;
        var meshFilter = gameobject.AddComponent<MeshFilter>();
        meshFilter.mesh = meshFilterMesh;
        var meshRenderer = gameobject.AddComponent<MeshRenderer>();
        meshRenderer.material = Material;

        meshDataJob.triangles.Dispose();
        meshDataJob.triangleCount.Dispose();
        meshDataJob.terrainMap.Dispose();
        terrainMapJob.terrainMap.Dispose();
    }
    
    public class chunkData
    {
        public int[] chunkTriangles;
        public Vector3[] chunkVertices;
        public Mesh Mesh;
    }
}
