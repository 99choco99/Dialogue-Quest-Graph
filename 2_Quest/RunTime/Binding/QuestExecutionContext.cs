using System;

namespace UniversalGraph
{
    /// <summary>어떤 퀘스트의 어떤 노드에서 이 메서드를 호출했는가? 를 알려주는 데이터 묶음</summary>
    public sealed class QuestExecutionContext
    {
        public QuestExecutionContext(IQuestController controller, QuestContainer container, QuestProgress progress, NodeBaseData nodeData)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            Container = container != null ? container : throw new ArgumentNullException(nameof(container), "실행 중인 Quest 정의가 필요합니다.");
            Progress = progress;
            NodeData = nodeData ?? throw new ArgumentNullException(nameof(nodeData), "실행 중인 Quest 노드가 필요합니다.");
        }

        /// <summary>
        /// 퀘스트 진행 기록을 보관하는 객체
        /// </summary>
        public IQuestController Controller { get; }

        /// <summary>
        /// 메서드를 호출한 퀘스트 그래프 에셋
        /// </summary>
        public QuestContainer Container { get; }

        /// <summary>
        /// 퀘스트의 현재 진행 기록
        /// </summary>
        public QuestProgress Progress { get; }

        /// <summary>
        /// 메서드를 호출한 노드의 데이터
        /// </summary>
        public NodeBaseData NodeData { get; }
    }
}
