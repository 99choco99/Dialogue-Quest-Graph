using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>선택적인 보상 Action을 실행하고 다음 노드로 진행</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/End/Reward")]
    public sealed class QuestRewardNode : QuestFlowNode<QuestRewardNodeData>
    {
        protected override string NodeTitle => !NodeData.RewardAction.HasKey ? "REWARD" : $"REWARD: {NodeData.RewardAction.Key}";

        /// <summary>실행할 타입 기반 보상 만들기</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new ();
            root.Add(new HelpBox("보상 Action만 실행합니다. Quest 상태 변경이 필요하면 State Change 노드를 연결하세요.", HelpBoxMessageType.Info));
            root.Add(MethodBindingInspector.Create(editHandler, "Optional Reward Action", NodeData.RewardAction, QuestMethodCatalog.GetMethodList(MethodKind.Action), RefreshTitle));
            return root;
        }
    }
}
