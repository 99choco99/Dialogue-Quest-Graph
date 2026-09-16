using System;
using UnityEngine;

namespace UniversalGraph
{
	/// <summary>대화 그래프 에셋 안에 있는 시작점 하나를 가리키고 있는 녀석</summary>
	[Serializable]
	public struct DialogueEntryPoint
	{
        /// <summary>
        /// 재생할 Dialogue 그래프 에셋
        /// </summary>
        public DialogueContainer Container;

        /// <summary>
        /// 지정할 EntryId. 비어 있으면 기본 Entry를 사용
        /// </summary>
        [SerializeField] private string entryId;


        /// <summary>빈 ID는 기본 Entry로 바꾸고 앞뒤 공백을 제거</summary>
        public string EntryId
		{
			get => string.IsNullOrWhiteSpace(entryId) ? DialogueEntryNodeData.DefaultEntryId : entryId.Trim();
			set => entryId = string.IsNullOrWhiteSpace(value) ? DialogueEntryNodeData.DefaultEntryId : value.Trim();
		}

		/// <summary>그냥 생성자</summary>
		public DialogueEntryPoint(DialogueContainer container, string entryId)
		{
			Container = container;
			this.entryId = string.IsNullOrWhiteSpace(entryId) ? DialogueEntryNodeData.DefaultEntryId : entryId.Trim();
		}
	}
}
