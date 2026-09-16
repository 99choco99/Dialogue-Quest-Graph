using System;
using UnityEngine;

namespace UniversalGraph
{
	/// <summary>Dialogue 그래프의 시작점을 제공하는 Quest 종착 노드</summary>
	[Serializable]
	public sealed class DialogueCandidateNodeData : NodeBaseData
	{
        /// <summary>
        /// 진입점 참조
        /// </summary>
        public DialogueEntryPoint EntryPoint;

		/// <summary>UI에 표시할 이름</summary>
        public string DisplayName = "Default";

        /// <summary>대화 후보 우선순위</summary>
        public int Priority = 0;
	}
}
