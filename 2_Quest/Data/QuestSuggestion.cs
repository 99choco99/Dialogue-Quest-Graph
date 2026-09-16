namespace UniversalGraph
{
    /// <summary>퀘스트 제안 정보</summary>
    public sealed class QuestSuggestion
    {
        internal QuestSuggestion(QuestContainer container, DialogueEntryPoint dialogueEntryPoint, int priority, bool isAvailable, string blockReason, string interactionEntryGuid, string suggestionNodeGuid)
        {
            Container = container;
			DialogueEntryPoint = dialogueEntryPoint;
            Priority = priority;
            IsAvailable = isAvailable;
            BlockReason = blockReason ?? string.Empty;
            InteractionEntryGuid = interactionEntryGuid;
            SuggestionNodeGuid = suggestionNodeGuid;
        }

        //=================================== 퀘스트에 대한 정보 =================================

        /// <summary>Quest 정의</summary>
        public QuestContainer Container { get; }

        /// <summary>Quest 정의의 고정 ID</summary>
        public int QuestId => Container.QuestId;

        /// <summary>Quest 목록에 표시할 이름</summary>
        public string Name => Container.questName;

        /// <summary>Quest 목록에 표시할 설명</summary>
        public string Description => Container.description;

        /// <summary>우선순위</summary>
        public int Priority { get; }

        /// <summary>조회 시점에 이 퀘스트를 수락할 수 있는지</summary>
        public bool IsAvailable { get; }

        /// <summary>수락할 수 없는 이유를 UI에 표시하기 위한 설명</summary>
        public string BlockReason { get; }


        //===================================노드의 참조값들=================================

        /// <summary>게임에서 필요에 따라 재생할 선택적인 대화 시작점</summary>
        public DialogueEntryPoint DialogueEntryPoint { get; }

        /// <summary>상호작용 시작점 노드 guid</summary>
        internal string InteractionEntryGuid { get; }

        /// <summary>Quest Suggestion 노드 guid</summary>
        internal string SuggestionNodeGuid { get; }
    }
}
