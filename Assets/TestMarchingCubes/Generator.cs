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

    private Transform parent;

    // Start is called before the first frame update
    void Start()
    {
        parent = new GameObject("Chunks").transform;
        
        for (int x = -120; x < 120; x+=32)
        // for (int y = -120; y < 120; y+=30)
        for (int z = -120; z < 120; z+=32)
        {
            var lod = 0;
            var distance = Vector3.Distance(new Vector3(x,0,z), new Vector3(120, 0, 0));
            if (distance > 130)
            {
                lod = 1;
            }
            if (distance > 200)
            {
                lod = 2;
            }
            if (distance > 300)
            {
                lod = 3;
            }
            CreateChunk(new Vector3(x,0,z), lod);
        }
    }

    private void CreateChunk(Vector3 chunkPosition, int lod)
    {
        var chunkSize = 33;
        var chunkSizeDoubled = chunkSize * 2;
        var terrainMapSize = chunkSizeDoubled * chunkSizeDoubled * chunkSize;

        
        var terrainMapJob = new TerrainMapJob
        {
            chunkSize = chunkSize,
            chunkPosition = chunkPosition,
            planetSize = 100,
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
        terrainMapJob.Schedule(chunkSize, 320).Complete();

        var meshDataJob = new TransvoxelMeshDataJob
        {
            chunkSize = chunkSize,
            lod = lod,
            terrainMap = new NativeArray<float>(terrainMapJob.terrainMap, Allocator.TempJob),
            triangles = new NativeArray<Triangle>(90000, Allocator.TempJob),
            triangleCount = new NativeArray<int>(1, Allocator.TempJob)
        };

        meshDataJob.Schedule().Complete();

        var meshDataSet = new chunkData();
        meshDataSet.chunkVertices = new Vector3[0];
        meshDataSet.chunkTriangles = new int[0];

        var triCount = meshDataJob.triangleCount[0];
        var triangles = meshDataJob.triangles.ToArray();
        for (int i = 0; i < triCount; i++)
        {
            var triangle = triangles[i];
            meshDataSet.chunkVertices = meshDataSet.chunkVertices.Concat(new[]
                {
                    triangle.vertex1,
                    triangle.vertex2,
                    triangle.vertex3
                })
                .ToArray();
            meshDataSet.chunkTriangles = meshDataSet.chunkTriangles.Concat(new[]
                {
                    i * 3,
                    i * 3 + 1,
                    i * 3 + 2
                })
                .ToArray();
        }

        if (lod > 0)
        {
            GenerateLodSeam(chunkSize, chunkPosition, terrainMapJob.terrainMap, lod);
        }

        var meshFilterMesh = new Mesh
        {
            vertices = meshDataSet.chunkVertices,
            triangles = meshDataSet.chunkTriangles,
        };

        meshFilterMesh.RecalculateNormals();
        meshDataSet.Mesh = meshFilterMesh;

        var gameobject = new GameObject($"LOD: {lod}");
        gameobject.transform.position = chunkPosition;
        gameobject.transform.parent = parent;
        var meshFilter = gameobject.AddComponent<MeshFilter>();
        meshFilter.mesh = meshFilterMesh;
        var meshRenderer = gameobject.AddComponent<MeshRenderer>();
        meshRenderer.material = Material;

        meshDataJob.triangles.Dispose();
        meshDataJob.triangleCount.Dispose();
        meshDataJob.terrainMap.Dispose();
        terrainMapJob.terrainMap.Dispose();
    }

    private void GenerateLodSeam(int chunkSize, Vector3 chunkPosition, NativeArray<float> terrainMap, int lod)
    {
        var generateLodSeamJob = new GenerateLodSeamJob
        {
            chunkSize = chunkSize,
            chunkPosition = chunkPosition,
            terrainMap = new NativeArray<float>(terrainMap, Allocator.TempJob),
            lod = lod,
            triangles = new NativeArray<Triangle>(90000, Allocator.TempJob),
            triangleCount = new NativeArray<int>(1, Allocator.TempJob),
            seed = 1,
            octaves = 1,
            weightedStrength = 1,
            lacunarity = 0.2f,
            gain = 0.3f,
        };
        
        generateLodSeamJob.Schedule().Complete();
        
        var meshDataSet = new chunkData();
        meshDataSet.chunkVertices = new Vector3[0];
        meshDataSet.chunkTriangles = new int[0];
        
        var triCount = generateLodSeamJob.triangleCount[0];
        var triangles = generateLodSeamJob.triangles.ToArray();
        for (int i = 0; i < triCount; i++)
        {
            var triangle = triangles[i];
            meshDataSet.chunkVertices = meshDataSet.chunkVertices.Concat(new[]
                {
                    triangle.vertex1,
                    triangle.vertex2,
                    triangle.vertex3
                })
                .ToArray();
            meshDataSet.chunkTriangles = meshDataSet.chunkTriangles.Concat(new[]
                {
                    i * 3,
                    i * 3 + 1,
                    i * 3 + 2
                })
                .ToArray();
        }
        
        var meshFilterMesh = new Mesh
        {
            vertices = meshDataSet.chunkVertices,
            triangles = meshDataSet.chunkTriangles,
        };
        
        meshFilterMesh.RecalculateNormals();
        meshDataSet.Mesh = meshFilterMesh;
        
        var gameobject = new GameObject($"LOD: {lod} Seam");
        gameobject.transform.position = chunkPosition;
        var meshFilter = gameobject.AddComponent<MeshFilter>();
        meshFilter.mesh = meshFilterMesh;
        var meshRenderer = gameobject.AddComponent<MeshRenderer>();
        meshRenderer.material = Material;
        
        generateLodSeamJob.triangles.Dispose();
        generateLodSeamJob.triangleCount.Dispose();
    }

    public class chunkData
    {
        public int[] chunkTriangles;
        public Vector3[] chunkVertices;
        public Mesh Mesh;
    }
}
