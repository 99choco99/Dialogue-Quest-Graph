using System;
using UnityEngine;

namespace UniversalGraph
{
	/// <summary>모든 그래프 노드가 공유하는 직렬화 데이터의 부모 클래스</summary>
	[Serializable]
	public abstract class NodeBaseData
	{
		/// <summary>고정 식별자</summary>
		public string Guid = System.Guid.NewGuid().ToString();

		/// <summary>에디터 캔버스에서 사용하는 노드 위치</summary>
		public Vector2 Position;
	}
}
