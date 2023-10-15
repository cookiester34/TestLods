using TestMarchingCubes;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TerrainBakery.Jobs
{
	[BurstCompile]
	public struct TransvoxelMeshDataJob : IJob, IChunkJob
	{
		[ReadOnly] public int chunkSize;
		[ReadOnly] public NativeArray<float> terrainMap;
		[ReadOnly] public int lod;
		
		public NativeArray<Triangle> triangles;
		public NativeArray<int> triangleCount;

		private int lodIncrement;
		
		[BurstCompile]
		public void Execute()
		{
			lodIncrement = TransvoxelTables.lodTable[lod];
			
			// Loop through each "cube" in our terrain.
			for (var x = 0; x < chunkSize - 2; x += lodIncrement)
			for (var y = 0; y < chunkSize - 2; y += lodIncrement)
			for (var z = 0; z < chunkSize - 2; z += lodIncrement)
			{
				if (lodIncrement != 1 && IsEdgeCube(x, y, z))
				{
					continue;
				}
				CreateCube(new Vector3Int(x, y, z));
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
			var cellValues = new float[8];
			
			for (var i = 0; i < 8; ++i) {
				Vector3Int voxelPosition = cellPosition + TransvoxelTables.RegularCornerOffset[i] * lodIncrement;
				cellValues[i] = SampleTerrainMap(voxelPosition);
			}

			var caseCode = (cellValues[0] > 0 ? 0x01 : 0)
							| (cellValues[1] > 0 ? 0x02 : 0)
							| (cellValues[2] > 0 ? 0x04 : 0)
							| (cellValues[3] > 0 ? 0x08 : 0)
							| (cellValues[4] > 0 ? 0x10 : 0)
							| (cellValues[5] > 0 ? 0x20 : 0)
							| (cellValues[6] > 0 ? 0x40 : 0)
							| (cellValues[7] > 0 ? 0x80 : 0);
 
			if (caseCode == 0 || caseCode == 255) {
				return;
			}
			
			CreateCubeTriangles(cellPosition, caseCode, cellValues);
		}

		private void CreateCubeTriangles(Vector3Int cellPosition, int caseCode, float[] cellValues)
		{
			int cellClass = TransvoxelTables.RegularCellClass[caseCode];
			ref var edgeCodes = ref TransvoxelTables.RegularVertexData[caseCode];
			ref var cellData = ref TransvoxelTables.RegularCellData[cellClass];

			var cellVertCount = cellData.GetVertexCount();
			var indices = cellData.GetIndices();
			
			var verts = new Vector3[cellVertCount];
			for (var i = 0; i < cellVertCount; i++)
			{
				var edgeCode = edgeCodes[i];

				var cornerIdx0 = (ushort)((edgeCode >> 4) & 0x0F);
				var cornerIdx1 = (ushort)(edgeCode & 0x0F);

				var density0 = cellValues[cornerIdx0];
				var density1 = cellValues[cornerIdx1];
				
				var t0 = density1 / (density1 - density0);
				var t1 = 1 - t0;
				
				var vertLocalPos0 = cellPosition + TransvoxelTables.RegularCornerOffset[cornerIdx0] * lodIncrement;
				var vertLocalPos1 = cellPosition + TransvoxelTables.RegularCornerOffset[cornerIdx1] * lodIncrement;

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

		[BurstCompile]
		private float SampleTerrainMap(Vector3Int corner)
		{
			return terrainMap[corner.x + chunkSize * (corner.y + chunkSize * corner.z)];
		}
	}
}