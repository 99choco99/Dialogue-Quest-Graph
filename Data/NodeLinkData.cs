using System;

namespace UniversalGraph
{
	/// <summary>두 그래프 노드의 포트를 잇는 직렬화 가능한 단방향 연결 정보입니다.</summary>
	[Serializable]
	public class NodeLinkData
	{
		/// <summary>출력 포트를 가진 노드의 GUID</summary>
		public string StartNodeGuid;

		/// <summary>출발 출력 포트의 고정 식별자</summary>
		public string StartPortName;

		/// <summary>도착 입력 포트를 가진 노드의 GUID</summary>
		public string TargetNodeGuid;

		/// <summary>
		/// 도착 입력 포트의 고정 식별자
		/// </summary>
		public string TargetPortName;
	}
}
