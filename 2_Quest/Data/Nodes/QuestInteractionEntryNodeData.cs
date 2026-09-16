using System;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>
    /// 상호작용 조회 시작점과 반응할 대상 ID를 저장하는 데이터
    /// </summary>
    [Serializable]
	public sealed class QuestInteractionEntryNodeData : NodeBaseData
	{
		[SerializeField]
		private string targetId = string.Empty;

		public string TargetId
		{
			get => targetId?.Trim() ?? string.Empty;
			set => targetId = value?.Trim() ?? string.Empty;
		}
	}
}
