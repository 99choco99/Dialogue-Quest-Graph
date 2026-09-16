using System.Collections.Generic;

namespace UniversalGraph
{
    /// <summary>
    /// Quest의 진행기록을 기록하고 처리할 Controller를 제작할 때 상속받아야할 인터페이스
    /// </summary>
    public interface IQuestController
	{
		/// <summary>고정 Quest ID로 찾을 수 있는 퀘스트 진행 기록</summary>
		IDictionary<int, QuestProgress> QuestProgress { get; }

		/// <summary>Quest 상태 또는 목표 진행량이 바뀌었음을 게임에 알림</summary>
		void OnQuestProgressChanged(QuestContainer container, QuestProgress progress);
	}
}
