using System;
using System.Collections.Generic;

namespace UniversalGraph
{
	[Serializable]
	public class QuestProgress
	{
		/// <summary>어떤 퀘스트에 대한 것인지</summary>
		public int questId;

        /// <summary>진행 기록에 대응하는 그래프 스키마 버전</summary>
        public int graphSchemaVersion;

		/// <summary>현재 Quest 진행 단계</summary>
		public QuestState state;

		/// <summary>초기화·재시작 전의 실행이 새 진행 기록을 덮어쓰지 않도록 구분하는 런타임 번호입니다.</summary>
		[NonSerialized]
		internal int runVersion;


		//=========================== 노드============================

        /// <summary>해당 퀘스트에서 현재 진행 중인 노드의 GUID 목록</summary>
        public List<string> ActiveNodeGuids { get; } = new();

        /// <summary>목표별 현재 진행량</summary>
        public Dictionary<string, int> ObjectiveAmounts { get; } = new();

		/// <summary>이미 실행한 일회성 흐름 노드. <para>
		/// </para>불러오기 후 보상이나 Action이 중복 실행되는 것을 막기 위함</summary>
		public List<string> CompletedNodeGuids { get; } = new();

        /// <summary>AND Gate에 도착한 출발지 기록</summary>
        public List<string> CompletedANDGateInputs { get; } = new();

		public QuestProgress() { }

		public QuestProgress(QuestContainer container)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container), "복원할 Quest 진행 데이터가 필요합니다.");
			}

			questId = container.QuestId;
			graphSchemaVersion = container.SchemaVersion;
			state = QuestState.NotStarted;
		}
	}
}
