using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>게임이 제공하는 Condition 결과를 통해 Quest 흐름을 분기</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/Condition/condition")]
    public sealed class QuestConditionNode : GraphNode<QuestConditionNodeData>
    {
        public override Vector2 DefaultSize => new(210f, 120f);

        /// <summary>입력 하나와 True, False 분기 만들기</summary>
        protected override void Draw()
        {
            RefreshTitle();
            Port input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            input.portName = QuestPortNames.Input;
            inputContainer.Add(input);

            AddOutput(QuestPortNames.True);
            AddOutput(QuestPortNames.False);

            AddToClassList("condition-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        private void AddOutput(string portName)
        {
            Port port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            port.portName = portName;
            outputContainer.Add(port);
        }

        private void RefreshTitle()
        {
            title = $"IF: {(!NodeData.Condition.HasKey ? "Unassigned" : NodeData.Condition.Key)}";
        }

        /// <summary>Attribute Condition 선택기 인스펙터에 만들기</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new ();
            root.Add(new HelpBox("코드 작성 없이 그래프에 연결하려면 Attribute가 붙은 메서드를 선택하세요.", HelpBoxMessageType.Info));

            root.Add(MethodBindingInspector.Create(editHandler, "Quest Condition", NodeData.Condition, QuestMethodCatalog.GetMethodList(MethodKind.Condition), RefreshTitle));

            return root;
        }
    }
}
