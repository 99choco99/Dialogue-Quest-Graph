using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>노드에 도달하면 프로젝트에서 정의한 Action을 실행</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/Flow/Action")]
    public sealed class QuestActionNode : QuestFlowNode<QuestActionNodeData>
    {
        protected override string NodeTitle => $"ACTION: {(!NodeData.Action.HasKey ? "Unassigned" : NodeData.Action.Key)}";

        /// <summary>Quest Attribute Action 선택기를 인스펙터에 만들기</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new ();
            root.Add(new HelpBox("코드 작성 없이 그래프에 연결하려면 Attribute가 붙은 메서드를 선택하세요.", HelpBoxMessageType.Info));
            root.Add(MethodBindingInspector.Create(editHandler, "Quest Action", NodeData.Action, QuestMethodCatalog.GetMethodList(MethodKind.Action), RefreshTitle));
            return root;
        }
    }
}
