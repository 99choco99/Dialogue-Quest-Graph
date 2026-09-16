namespace UniversalGraph
{
	/// <summary>Quest 진행 단계</summary>
	public enum QuestState
	{
		NotStarted = 0,
		InProgress = 1,
		CanComplete = 2,
		TurnedIn = 3,
		Failed = 4,
		/// <summary>그래프나 메서드 실행 오류로 중단</summary>
		ExecutionError = 5
	}
}
