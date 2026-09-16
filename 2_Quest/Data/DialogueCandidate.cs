namespace UniversalGraph
{
    /// <summary>게임 코드에 반환하는 조회 시점의 대화 후보 정보. 원본 노드 데이터와 분리해서 보관합니다.</summary>
    public sealed class DialogueCandidate
    {
        internal DialogueCandidate(DialogueEntryPoint entryPoint, string displayName, int priority)
        {
            EntryPoint = entryPoint;
            DisplayName = displayName;
            Priority = priority;
        }

        /// <summary>선택한 후보의 대화를 시작할 진입점</summary>
        public DialogueEntryPoint EntryPoint { get; }

        /// <summary>목록에 표시할 이름</summary>
        public string DisplayName { get; }

        /// <summary>게임에서 후보의 표시 순서나 선택 기준으로 사용할 우선순위</summary>
        public int Priority { get; }
    }
}
