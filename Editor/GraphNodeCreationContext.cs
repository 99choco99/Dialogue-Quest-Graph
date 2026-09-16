using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalGraph.Editor
{
	/// <summary>새 노드의 초기 데이터를 만들 때 필요한 캔버스 위치와 기존 노드 목록</summary>
	public readonly struct GraphNodeCreationContext
	{
		/// <summary>생성할 노드의 캔버스 위치</summary>
		public Vector2 Position { get; }

		/// <summary>현재 캔버스에 이미 존재하는 노드 데이터</summary>
		public IReadOnlyList<NodeBaseData> ExistingNodes { get; }

		public GraphNodeCreationContext(Vector2 position, IReadOnlyList<NodeBaseData> existingNodes)
		{
			Position = position;
			ExistingNodes = existingNodes ?? Array.Empty<NodeBaseData>();
		}
	}
}
