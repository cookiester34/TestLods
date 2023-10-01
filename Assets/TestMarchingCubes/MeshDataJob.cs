using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static LookupTable;

namespace TerrainBakery.Jobs
{
	public struct CubeEdgeTriangleData
	{
		public int count;
		public Vector3 vertex1;
		public Vector3 vertex2;
		public Vector3 vertex3;
		public Vector3 vertex4;
	}
	
	// [BurstCompile]
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

		private bool isLowerLod;
		private int lowerLodCubeCount;
		public NativeArray<Vector3> lowerLodData;
		private int higherLodCubeCount;
		public NativeArray<Vector3> higherLodData;

		private bool wasAdded;

		[BurstCompile]
		public void Execute()
		{
			var lodIncrement = LodTable[lodIndex];
			var isLod0 = lodIndex == 0;
			isLowerLod = false;

			if (!isLod0)
			{
				for (var index = 0; index < lowerLodData.Length; index++)
				{
					lowerLodData[index] = new Vector3(-1, -1, -1);
				}
				for (var index = 0; index < higherLodData.Length; index++)
				{
					higherLodData[index] = new Vector3(-1, -1, -1);
				}
			}

			// Loop through each "cube" in our terrain.
			for (var x = 0; x < chunkSize - 1; x += lodIncrement)
			for (var y = 0; y < chunkSize - 1; y += lodIncrement)
			for (var z = 0; z < chunkSize - 1; z += lodIncrement)
			{
				if (!isLod0 && IsEdgeCube(x, y, z, lodIncrement)) continue;
				CreateCube(x, y, z, lodIncrement);
			}
			
			if (wasAdded) Debug.Log("Was Added");

			if (!isLod0)
			{
				isLowerLod = true;
				for (var x = 0; x < chunkSize - 1; x++)
				for (var y = 0; y < chunkSize - 1; y++)
				for (var z = 0; z < chunkSize - 1; z++)
				{
					if (IsEdgeCube(x, y, z))
					{
						CreateCube(x, y, z, 1);
					}
				}
				
				// for (var index = 0; index < lowerLodData.Length; index++)
				// {
				// 	Debug.Log(lowerLodData[index]);
				// }
				// for (var index = 0; index < higherLodData.Length; index++)
				// {
				// 	Debug.Log(higherLodData[index]);
				// }

				var lower = new List<CubeEdgeTriangleData>();
				var higher = new List<CubeEdgeTriangleData>();

				Vector3 vertex1 = default;
				Vector3 vertex2 = default;
				Vector3 vertex3 = default;
				Vector3 vertex4 = default;
				var count = 0;

				for (var index = 0; index < lowerLodCubeCount; index++)
				{
					var vector3 = lowerLodData[index];
					if (vector3 == new Vector3(-2, -2, -2))
					{
						lower.Add(new CubeEdgeTriangleData()
						{
							vertex1 = vertex1,
							vertex2 = vertex2,
							vertex3 = vertex3,
							vertex4 = vertex4,
							count = count
						});
						count = 0;
						vertex1 = default;
						vertex2 = default;
						vertex3 = default;
						vertex4 = default;
						continue;
					}

					if (vertex1 == default)
					{
						vertex1 = vector3;
						count++;
						continue;
					}

					if (vertex2 == default)
					{
						vertex2 = vector3;
						count++;
						continue;
					}

					if (vertex3 == default)
					{
						vertex3 = vector3;
						count++;
						continue;
					}

					if (vertex4 == default)
					{
						vertex4 = vector3;
						count++;
					}
				}
				
				vertex1 = default;
				vertex2 = default;
				vertex3 = default;
				vertex4 = default;
				count = 0;

				for (var index = 0; index < higherLodCubeCount; index++)
				{
					var vector3 = higherLodData[index];
					if (vector3 == new Vector3(-2, -2, -2))
					{
						higher.Add(new CubeEdgeTriangleData()
						{
							vertex1 = vertex1,
							vertex2 = vertex2,
							vertex3 = vertex3,
							vertex4 = vertex4,
							count = count
						});
						count = 0;
						vertex1 = default;
						vertex2 = default;
						vertex3 = default;
						vertex4 = default;
						continue;
					}

					if (vertex1 == default)
					{
						vertex1 = vector3;
						count++;
						continue;
					}

					if (vertex2 == default)
					{
						vertex2 = vector3;
						count++;
						continue;
					}

					if (vertex3 == default)
					{
						vertex3 = vector3;
						count++;
						continue;
					}

					if (vertex4 == default)
					{
						vertex4 = vector3;
						count++;
					}
				}

				Debug.Log(lower.Count);
				Debug.Log(higher.Count);

				var lowerCount = 0;
				for (var index = 0; index < higher.Count; index++)
				{
					var cubeEdgeTriangleData = higher[index];
					if (cubeEdgeTriangleData.vertex1 == default) continue;
					
					Vector3 newVert = default;
					var needsNewVert = cubeEdgeTriangleData.count < 2;
					if (needsNewVert)
					{
						//if we only have 1 vertex, we create a new point
						newVert = (cubeEdgeTriangleData.vertex1 + higher[index].vertex1) / 2;
					}

					var lowerCopy = Mathf.Min(lowerCount + lodIncrement, lower.Count);
					for (int i = lowerCount; i < lowerCopy; i++)
					{
						var lowerCube = lower[i];
					
						//1st triangle
						var vert1 = lowerCube.vertex1;
						var vert2 = cubeEdgeTriangleData.vertex1;
						var vert3 = lowerCube.vertex2;

						vertices[vertCount[0]] = vert1;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						vertices[vertCount[0]] = vert2;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						vertices[vertCount[0]] = vert3;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
					
						//2nd triangle
						var vert4 = lowerCube.vertex2;
						var vert5 = cubeEdgeTriangleData.vertex1;
						var vert6 = needsNewVert ? newVert : cubeEdgeTriangleData.vertex2;
						
						vertices[vertCount[0]] = vert4;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						vertices[vertCount[0]] = vert5;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						vertices[vertCount[0]] = vert6;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
					
						//3rd triangle
						if (lowerCube.vertex3 == default) continue;
						var vert7 = lowerCube.vertex2;
						var vert8 = cubeEdgeTriangleData.vertex2;
						var vert9 = lowerCube.vertex3;
						
						vertices[vertCount[0]] = vert7;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						vertices[vertCount[0]] = vert8;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						vertices[vertCount[0]] = vert9;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						
						//4th triangle
						if (cubeEdgeTriangleData.vertex3 == default) continue;
						var vert10 = lowerCube.vertex3;
						var vert11 = cubeEdgeTriangleData.vertex2;
						var vert12 = cubeEdgeTriangleData.vertex3;
						
						vertices[vertCount[0]] = vert10;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						vertices[vertCount[0]] = vert11;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						vertices[vertCount[0]] = vert12;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						
						//5th triangle
						if (lowerCube.vertex4 == default) continue;
						var vert13 = lowerCube.vertex3;
						var vert14 = cubeEdgeTriangleData.vertex3;
						var vert15 = lowerCube.vertex4;
						
						vertices[vertCount[0]] = vert13;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						vertices[vertCount[0]] = vert14;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
						vertices[vertCount[0]] = vert15;
						triangles[triCount[0]] = vertCount[0];
						vertCount[0]++;
						triCount[0]++;
					}

					lowerCount += lodIncrement;
				}
			}
		}
		
		private bool IsEdgeCube(int x, int y, int z, int lodIncrement = 1)
		{
			return x == 0 || x == chunkSize - lodIncrement - 1 ||
			       y == 0 || y == chunkSize - lodIncrement - 1 ||
			       z == 0 || z == chunkSize - lodIncrement - 1;
		}

		private void AddToLodData(Vector3 vertPosition, int lodIncrement)
		{
			var increment = lodIndex == 0 ? 1 : lodIncrement;
			if (vertPosition.x == increment || vertPosition.x == chunkSize - increment - 1 ||
			    vertPosition.y == increment || vertPosition.y == chunkSize - increment - 1 || 
			    vertPosition.z == increment || vertPosition.z == chunkSize - increment - 1)
			{
				if (isLowerLod)
				{
					lowerLodData[lowerLodCubeCount] = vertPosition;
					lowerLodCubeCount++;
				}
				else
				{
					higherLodData[higherLodCubeCount] = vertPosition;
					higherLodCubeCount++;
				}
			}
		}
		
		private void FinishCube()
		{
			if (isLowerLod)
			{
				lowerLodData[lowerLodCubeCount] = new Vector3(-2,-2,-2);
				lowerLodCubeCount++;
			}
			else
			{
				higherLodData[higherLodCubeCount] = new Vector3(-2,-2,-2);
				higherLodCubeCount++;
			}
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
			FinishCube();
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
					AddToLodData(vertPosition, lodIncrement);
					vertices[vertCount[0]] = vertPosition;
					triangles[triCount[0]] = vertCount[0];
					vertCount[0]++;
				}
				else
				{
					triangles[triCount[0]] = VertForIndice(vertPosition, lodIncrement);
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
		private int VertForIndice(Vector3 vertPosition, int lodIncrement)
		{
			// Loop through all the vertices currently in the vertices list.
			var vCount = vertCount[0];
			for (var index = 0; index < vCount; index++)
			{
				// If we find a vert that matches ours, then simply return this index.
				if (vertices[index] == vertPosition)
				{
					AddToLodData(vertPosition, lodIncrement);
					return index;
				}
			}

			// If we didn't find a match, add this vert to the list and return last index.
			AddToLodData(vertPosition, lodIncrement);
			vertices[vCount] = vertPosition;
			vertCount[0]++;
			return vCount;
		}
	}
}