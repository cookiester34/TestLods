using UnityEngine;

namespace TestMarchingCubes
{
	public struct Triangle
	{
		public Vector3 vertex1;
		public Vector3 vertex2;
		public Vector3 vertex3;
		
		public Triangle(Vector3 vertex1, Vector3 vertex2, Vector3 vertex3)
		{
			this.vertex1 = vertex1;
			this.vertex2 = vertex2;
			this.vertex3 = vertex3;
		}
	}
}