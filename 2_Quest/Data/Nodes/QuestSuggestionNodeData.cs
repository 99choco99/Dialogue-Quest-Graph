using System;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>퀘스트를 제안하고 수락 여부를 물어볼 수 있도록 정보를 제공하는 노드</summary>
    [Serializable]
    public sealed class QuestSuggestionNodeData : NodeBaseData
    {
        /// <summary>
        /// Quest 선택 뒤 재생할 선택적인 Dialogue 그래프입니다. 비워 두면 UI에서 바로 수락
        /// </summary>
		public DialogueEntryPoint DialogueEntryPoint;

        /// <summary>
        /// 여러 Quest 선택 항목을 정렬하거나 자동 선택할 때 사용할 우선순위 값
        /// </summary>
        public int Priority;

        /// <summary>
        /// false면 UI에 선택할 수 없는 Quest와 차단 이유를 제공할 수 있음.
        /// </summary>
        public bool IsAvailable = true;

        /// <summary>
        /// 수락할 수 없는 이유. Is Available이 꺼진 경우에만 사용 가능
        /// </summary>
        public string BlockReason;
    }
}
