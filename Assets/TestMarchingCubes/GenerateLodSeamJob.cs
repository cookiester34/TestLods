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
			for (var x = 0; x < chunkSize - 2; x += lodIncrement)
			for (var y = 0; y < chunkSize - 2; y += lodIncrement)
			{
				if (IsEdgeCube(x, y, 0))
				{
					CreateCube(new Vector3Int(x, y, 0));
				}
			}
		}
		
		private bool IsEdgeCube(int x, int y, int z)
		{
			return x == 0 || x == chunkSize - lodIncrement ||
			       y == 0 || y == chunkSize - lodIncrement ||
			       z == 0 || z == chunkSize - lodIncrement;
		}

		private void CreateCube(Vector3Int cellPosition)
		{
			var trCellValues = new float[13];
			
			Vector3Int position0 = cellPosition + TransvoxelTables.TransitionCornerOffset[0] * lodIncrement;
			Vector3Int position2 = cellPosition + TransvoxelTables.TransitionCornerOffset[2] * lodIncrement;
			Vector3Int position6 = cellPosition + TransvoxelTables.TransitionCornerOffset[6] * lodIncrement;
			Vector3Int position8 = cellPosition + TransvoxelTables.TransitionCornerOffset[8] * lodIncrement;
			Vector3Int position9 = cellPosition + TransvoxelTables.TransitionCornerOffset[9] * lodIncrement;
			Vector3Int positionA = cellPosition + TransvoxelTables.TransitionCornerOffset[10] * lodIncrement;
			Vector3Int positionB = cellPosition + TransvoxelTables.TransitionCornerOffset[11] * lodIncrement;
			Vector3Int positionC = cellPosition + TransvoxelTables.TransitionCornerOffset[12] * lodIncrement;
			
			Vector3Int position1 = cellPosition + TransvoxelTables.TransitionCornerOffset[1] * lodIncrement;
			Vector3Int position3 = cellPosition + TransvoxelTables.TransitionCornerOffset[3] * lodIncrement;
			Vector3Int position4 = cellPosition + TransvoxelTables.TransitionCornerOffset[4] * lodIncrement;
			Vector3Int position5 = cellPosition + TransvoxelTables.TransitionCornerOffset[5] * lodIncrement;
			Vector3Int position7 = cellPosition + TransvoxelTables.TransitionCornerOffset[7] * lodIncrement;
			
			trCellValues[0] = SampleTerrainMap(position0);
			trCellValues[2] = SampleTerrainMap(position2);
			trCellValues[6] = SampleTerrainMap(position6);
			trCellValues[8] = SampleTerrainMap(position8);
			trCellValues[9] = SampleTerrainMap(position9);
			trCellValues[10] = SampleTerrainMap(positionA);
			trCellValues[11] = SampleTerrainMap(positionB);
			trCellValues[12] = SampleTerrainMap(positionC);
			
			trCellValues[1] = SampleTerrainMap(position1);
			trCellValues[3] = SampleTerrainMap(position3);
			trCellValues[4] = SampleTerrainMap(position4);
			trCellValues[5] = SampleTerrainMap(position5);
			trCellValues[7] = SampleTerrainMap(position7);
			
			int caseCode = (trCellValues[0] < 0 ? 1 : 0)
			               | (trCellValues[1] < 0 ? 2 : 0)
			               | (trCellValues[2] < 0 ? 4 : 0)
			               | (trCellValues[5] < 0 ? 8 : 0)
			               | (trCellValues[8] < 0 ? 16 : 0)
			               | (trCellValues[7] < 0 ? 32 : 0)
			               | (trCellValues[6] < 0 ? 64 : 0)
			               | (trCellValues[3] < 0 ? 128 : 0)
			               | (trCellValues[4] < 0 ? 256 : 0);
			
			if (caseCode == 0 || caseCode == 511) {
				return;
			}
			
			CreateCubeTriangles(cellPosition, caseCode, trCellValues);
		}

		private float GetValue(Vector3 position)
		{
			return FastNoiseLite.GetNoise(
				position.x,
				position.z,
				position.y,
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