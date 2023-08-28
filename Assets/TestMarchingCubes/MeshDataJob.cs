using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static LookupTable;

namespace TerrainBakery.Jobs
{
	[BurstCompile]
	public struct MeshDataJob : IJob, IChunkJob
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

		public NativeArray<float> cube;
		
		public NativeArray<int> triCount;
		public NativeArray<int> vertCount;
		
		public NativeArray<Vector3> vertices;
		public NativeArray<int> triangles;

		[BurstCompile]
		public void Execute()
		{
			var lodIncrement = LodTable[lodIndex];

			// Loop through each "cube" in our terrain.
			for (var x = 0; x < chunkSize - 1; x += lodIncrement)
			for (var y = 0; y < chunkSize - 1; y += lodIncrement)
			for (var z = 0; z < chunkSize - 1; z += lodIncrement)
			{
				// if (lodIndex != 0 && IsEdgeCube(x, y, z, lodIncrement)) continue;
				CreateCube(x, y, z, lodIncrement);
			}
			
			// if (lodIndex != 0)
			// {
			// 	for (var x = 0; x < chunkSize - 1; x++)
			// 	for (var y = 0; y < chunkSize - 1; y++)
			// 	for (var z = 0; z < chunkSize - 1; z++)
			// 	{
			// 		if (IsEdgeCube(x, y, z))
			// 		{
			// 			CreateCube(x, y, z, 1);
			// 		}
			// 	}
			// }
		}

		private void CreateCube(int x, int y, int z, int lodIncrement)
		{
			// Create an array of floats representing each corner of a cube and get the value from our terrainMap.
			for (var i = 0; i < 8; i++)
			{
				var terrain = SampleTerrainMap(new int3(x, y, z) + CornerTable[i] * lodIncrement);
				cube[i] = terrain;
			}

			// Pass the value into our MarchCube function.
			var position = new float3(x, y, z);
			// Get the configuration index of this cube.
			// Starting with a configuration of zero, loop through each point in the cube and check if it is below the terrain surface.
			var cubeIndex = 0;
			for (var i = 0; i < 8; i++)
				// If it is, use bit-magic to the set the corresponding bit to 1. So if only the 3rd point in the cube was below
				// the surface, the bit would look like 00100000, which represents the integer value 32.
				if (cube[i] > terrainSurface)
					cubeIndex |= 1 << i;


			// If the configuration of this cube is 0 or 255 (completely inside the terrain or completely outside of it) we don't need to do anything.
			if (cubeIndex is 0 or 255)
				return;

			// Loop through the triangles. There are never more than 5 triangles to a cube and only three vertices to a triangle.
			MarchCube(cubeIndex, position, lodIncrement);
		}

		private bool IsEdgeCube(int x, int y, int z, int lodIncrement = 1)
		{
			return x == 0 || x == chunkSize - lodIncrement - 1 ||
			       y == 0 || y == chunkSize - lodIncrement - 1 ||
			       z == 0 || z == chunkSize - lodIncrement - 1;
		}

		[BurstCompile]
		private void MarchCube(int cubeIndex, float3 position, int lodIncrement)
		{
			var edgeIndex = 0;
			for (var i = 0; i < 5; i++)
			for (var p = 0; p < 3; p++)
			{
				// Get the current indie. We increment triangleIndex through each loop.
				//x * 16 + y = val
				var indice = TriangleTable[cubeIndex * 16 + edgeIndex];

				// If the current edgeIndex is -1, there are no more indices and we can exit the function.
				if (indice == -1)
					return;

				// Get the vertices for the start and end of this edge.
				//x * 2 + y = val
				var vert1 = position + CornerTable[EdgeIndexes[indice * 2 + 0]] * lodIncrement;
				var vert2 = position + CornerTable[EdgeIndexes[indice * 2 + 1]] * lodIncrement;

				Vector3 vertPosition;
				if (smoothTerrain)
				{
					// Get the terrain values at either end of our current edge from the cube array created above.
					var vert1Sample = cube[EdgeIndexes[indice * 2 + 0]];
					var vert2Sample = cube[EdgeIndexes[indice * 2 + 1]];

					// Calculate the difference between the terrain values.
					var difference = vert2Sample - vert1Sample;

					// If the difference is 0, then the terrain passes through the middle.
					if (difference == 0)
						difference = terrainSurface;
					else
						difference = (terrainSurface - vert1Sample) / difference;

					// Calculate the point along the edge that passes through.
					vertPosition = vert1 + difference * (vert2 - vert1);
				}
				else
				{
					// Get the midpoint of this edge.
					vertPosition = (vert1 + vert2) / 2f;
				}

				// Add to our vertices and triangles list and increment the edgeIndex.
				if (flatShaded)
				{
					vertices[vertCount[0]] = vertPosition;
					triangles[triCount[0]] = vertCount[0];
					vertCount[0]++;
				}
				else
				{
					triangles[triCount[0]] = VertForIndice(vertPosition);
				}

				edgeIndex++;
				triCount[0]++;
			}
		}

		[BurstCompile]
		private float SampleTerrainMap(int3 corner)
		{
			return terrainMap[corner.x + chunkSize * (corner.y + chunkSize * corner.z)];
		}

		[BurstCompile]
		private int VertForIndice(Vector3 vert)
		{
			// Loop through all the vertices currently in the vertices list.
			var vCount = vertCount[0];
			for (var index = 0; index < vCount; index++)
			{
				// If we find a vert that matches ours, then simply return this index.
				if (vertices[index] == vert) return index;
			}

			// If we didn't find a match, add this vert to the list and return last index.
			vertices[vCount] = vert;
			vertCount[0]++;
			return vCount;
		}
	}
}