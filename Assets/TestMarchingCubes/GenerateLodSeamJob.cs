using TerrainBakery.Helpers;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TestMarchingCubes
{
	public struct GenerateLodSeamJob : IJob
	{
		[ReadOnly] public int chunkSize;
		[ReadOnly] public Vector3 chunkPosition;
		[ReadOnly] public NativeArray<float> terrainMap;
		[ReadOnly] public int lod;
		[ReadOnly] public int seed;
		[ReadOnly] public int octaves;
		[ReadOnly] public float weightedStrength;
		[ReadOnly] public float lacunarity;
		[ReadOnly] public float gain;
		
		public NativeArray<Triangle> triangles;
		public NativeArray<int> triangleCount;
		
		private int lodIncrement;

		public void Execute()
		{
			lodIncrement = TransvoxelTables.lodTable[lod];
			
			// Loop through each "cube" in our terrain.
			for (var x = 0; x < chunkSize - 1; x += lodIncrement)
			for (var y = 0; y < chunkSize - 1; y += lodIncrement)
			{
				if (IsEdgeCube(x, y, 0))
				{
					CreateCube(new Vector3Int(x, y, 0));
				}
			}
		}
		
		private bool IsEdgeCube(int x, int y, int z)
		{
			return x == 0 || x == chunkSize - lodIncrement - 1 ||
			       y == 0 || y == chunkSize - lodIncrement - 1 ||
			       z == 0 || z == chunkSize - lodIncrement - 1;
		}

		private void CreateCube(Vector3Int cellPosition)
		{
			var trCellValues = new float[9];
			for (int i = 0; i < 9; i++)
			{
				var voxelPosition = cellPosition + TransvoxelTables.TransitionCornerOffset[i] * lodIncrement;
				trCellValues[i] = SampleTerrainMap(voxelPosition);
			}
			
			int caseCode = ((trCellValues[0] < 0 ? 1 : 0)
			                | (trCellValues[1] < 0 ? 2 : 0)
			                | (trCellValues[2] < 0 ? 4 : 0)
			                | (trCellValues[5] < 0 ? 8 : 0)
			                | (trCellValues[8] < 0 ? 16 : 0)
			                | (trCellValues[7] < 0 ? 32 : 0)
			                | (trCellValues[6] < 0 ? 64 : 0)
			                | (trCellValues[3] < 0 ? 128 : 0)
			                | (trCellValues[4] < 0 ? 256 : 0));
			
			if (caseCode == 0 || caseCode == 511) {
				return;
			}
			
			CreateCubeTriangles(cellPosition, caseCode, trCellValues);
		}

		private float GetValue(Vector3 position)
		{
			return FastNoiseLite.GetNoise(
				position.x,
				position.y,
				position.z,
				seed,
				octaves,
				weightedStrength,
				lacunarity,
				gain);
		}
		
		[BurstCompile]
		private float SampleTerrainMap(Vector3Int corner)
		{
			return terrainMap[corner.x + chunkSize * (corner.y + chunkSize * corner.z)];
		}
		
		private void CreateCubeTriangles(Vector3Int cellPosition, int caseCode, float[] trCellValues)
		{
			int cellClass = TransvoxelTables.TransitionCellClass[caseCode];
			ref var edgeCodes = ref TransvoxelTables.TransitionVertexData[caseCode];
			ref var cellData = ref TransvoxelTables.TransitionRegularCellData[cellClass & 0x7F];

			var cellVertCount = cellData.GetVertexCount();
			var indices = cellData.GetIndices();
			
			var verts = new Vector3[cellVertCount];
			for (var i = 0; i < cellVertCount; i++)
			{
				var edgeCode = edgeCodes[i];

				ushort cornerIdx0 = (ushort)((edgeCode >> 4) & 0x0F);
				ushort cornerIdx1 = (ushort)(edgeCode & 0x0F);

				float density0 = trCellValues[cornerIdx0];
				float density1 = trCellValues[cornerIdx1];
				
				var t0 = density1 / (density1 - density0);
				var t1 = 1 - t0;
				
				var vertLocalPos0 = cellPosition + TransvoxelTables.TransitionCornerOffset[cornerIdx0] * lodIncrement;
				var vertLocalPos1 = cellPosition + TransvoxelTables.TransitionCornerOffset[cornerIdx1] * lodIncrement;

				Vector3 vert0Copy = vertLocalPos0;
				Vector3 vert1Copy = vertLocalPos1;

				var vertex = vert0Copy * t0 + vert1Copy * t1;
				verts[i] = vertex;
			}

			var cellTriCount = cellData.GetTriangleCount();
			for (var i = 0; i < cellTriCount; i++)
			{
				triangles[triangleCount[0]] = new Triangle(
					verts[indices[i * 3]],
					verts[indices[i * 3 + 1]],
					verts[indices[i * 3 + 2]]
				);
				triangleCount[0]++;
			}
		}
	}
}