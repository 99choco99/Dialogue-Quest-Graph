using System;
using UnityEngine;

namespace UniversalGraph
{
	/// <summary>다른 Quest의 상태를 검사</summary>
	[Serializable]
	public sealed class QuestStateConditionNodeData : NodeBaseData
	{
        /// <summary>
        /// 상태를 검사할 Quest의 고정 ID
        /// </summary>
        public int QuestId;

        /// <summary>
        /// 원하는 Quest 상태
        /// </summary>
        public QuestState TargetState;
	}
}
