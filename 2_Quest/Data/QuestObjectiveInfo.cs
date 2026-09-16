namespace UniversalGraph
{
	/// <summary>게임 코드나 UI가 그래프를 직접 탐색하지 않고 읽을 수 있는 현재 목표 정보</summary>
	public sealed class QuestObjectiveInfo
	{
		internal QuestObjectiveInfo(int questId, QuestObjectiveNodeData objectiveData, int currentAmount)
		{
			QuestId = questId;
			NodeGuid = objectiveData.Guid;
			EventKey = objectiveData.EventKey;
			TargetId = objectiveData.TargetId;
			TargetReference = objectiveData.TargetReference;
			Description = objectiveData.ObjectiveDescription;
			CurrentAmount = currentAmount;
			RequiredAmount = objectiveData.RequiredAmount;
		}

        //==================퀘스트 정보==================
        public int QuestId { get; }
        public string Description { get; }

        //==================노드 관련 정보==================

        public string NodeGuid { get; }
		public string EventKey { get; }

		//==================목표에 해당하는 것==================
        public int TargetId { get; }
        public UnityEngine.Object TargetReference { get; }
        public int CurrentAmount { get; }
		public int RequiredAmount { get; }
	}
}
