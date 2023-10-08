using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using static LookupTableTransvoxel;

namespace TerrainBakery.Jobs
{
	// [BurstCompile]
	public struct TransvoxelMeshDataJob : IJob, IChunkJob
	{
		[ReadOnly]
		public bool smoothTerrain;

		[ReadOnly]
		public bool flatShaded;

		[ReadOnly]
		public int chunkSize;

		[ReadOnly]
		public NativeArray<float> terrainMap;

		[ReadOnly]
		public float terrainSurface;

		[ReadOnly]
		public int lodIndex;

		[FormerlySerializedAs("cube")]
		public NativeArray<int> density;
		
		public NativeArray<int> triCount;
		public NativeArray<int> vertCount;
		
		public NativeArray<Vector3> vertices;
		public NativeArray<int> triangles;

		private const float s = 1f / 256f;
		private static int[] vertexIndices = new int[16];

		[BurstCompile]
		public void Execute()
		{
			var lodIncrement = LodTable[lodIndex];
			var isLod0 = lodIndex == 0;

			// Loop through each "cube" in our terrain.
			for (var x = 0; x < chunkSize - 1; x += lodIncrement)
			for (var y = 0; y < chunkSize - 1; y += lodIncrement)
			for (var z = 0; z < chunkSize - 1; z += lodIncrement)
			{
				if (!isLod0 && IsEdgeCube(x, y, z, lodIncrement)) continue;
				CreateCube(x, y, z, lodIncrement);
			}

			if (!isLod0)
			{
				for (var x = 0; x < chunkSize - 1; x++)
				for (var y = 0; y < chunkSize - 1; y++)
				for (var z = 0; z < chunkSize - 1; z++)
				{
					if (IsEdgeCube(x, y, z))
					{
						CreateCube(x, y, z, 1);
					}
				}
			}
		}
		
		[BurstCompile]
		private bool IsEdgeCube(int x, int y, int z, int lodIncrement = 1)
		{
			return x == 0 || x == chunkSize - lodIncrement - 1 ||
			       y == 0 || y == chunkSize - lodIncrement - 1 ||
			       z == 0 || z == chunkSize - lodIncrement - 1;
		}
		
		[BurstCompile]
		private void CreateCube(int x, int y, int z, int lodIncrement)
		{
			// Create an array of floats representing each corner of a cube and get the value from our terrainMap.
			for (var i = 0; i < 8; i++)
			{
				var terrain = SampleTerrainMap(new int3(x, y, z) + cornerIndex[i] * lodIncrement);
				density[i] = Mathf.RoundToInt(Mathf.Abs(terrain));
			}

			var cubeIndex = 0;
			for (var i = 0; i < 8; i++)
				// If it is, use bit-magic to the set the corresponding bit to 1. So if only the 3rd point in the cube was below
				// the surface, the bit would look like 00100000, which represents the integer value 32.
				if (density[i] > terrainSurface)
					cubeIndex |= 1 << i;


			// If the configuration of this cube is 0 or 255 (completely inside the terrain or completely outside of it) we don't need to do anything.
			if (cubeIndex is 0 or 255)
				return;

			byte cellClass = RegularCellClass[cubeIndex];
			int[] vertexLocations = RegularVertexData[cubeIndex];

			long vertexCount = Geos[cellClass] >> 4;
			long triangleCount = Geos[cellClass] & 0x0F;

			for (int i = 0; i < vertexCount; i++)
			{
				ushort edge = (ushort)(vertexLocations[i] & 255);
				byte v0 = (byte)((edge >> 4) & 0x0F); //First Corner Index
				byte v1 = (byte)(edge & 0x0F); //Second Corner Index

				int3 cornerOffset = density[v0] == 0 
					? cornerIndex [v0]
					: cornerIndex [v1];

				int vertPosX = x + cornerOffset.x;
				int vertPosY = y + cornerOffset.y;
				int vertPosZ = z + cornerOffset.z;

				vertices[vertCount[0]] = new Vector3(vertPosX, vertPosY, vertPosZ);
				vertCount[0]+=1;
				vertexIndices[i] = vertCount[0] - 1;
			}

			var triangleIndices = LookupTableTransvoxel.RegularCellData[cellClass].vertexIndex;
			for (int i = 0; i < triangleCount; i += 3)
			{
				triangles[triCount[0]] = vertexIndices[triangleIndices[i]];
				triangles[triCount[0] + 1] = vertexIndices[triangleIndices[i + 1]];
				triangles[triCount[0] + 2] = vertexIndices[triangleIndices[i + 2]];
				triCount[0]+=3;
			}
		}
		
		[BurstCompile]
		private float SampleTerrainMap(int3 corner)
		{
			return terrainMap[corner.x + chunkSize * (corner.y + chunkSize * corner.z)];
		}
	}
}