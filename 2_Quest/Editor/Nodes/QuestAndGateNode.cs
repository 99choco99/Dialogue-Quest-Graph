using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>서로 다른 모든 입력 분기가 도착하면 다음 흐름으로 진행</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/Flow/AND Gate")]
    public sealed class QuestAndGateNode : QuestFlowNode<QuestAndGateNodeData>
    {
        protected override string NodeTitle => "AND GATE";

        /// <summary>인스펙터 설명</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            return new HelpBox("서로 다른 모든 입력 분기가 도착하면 진행합니다.", HelpBoxMessageType.Info);
        }
    }
}
