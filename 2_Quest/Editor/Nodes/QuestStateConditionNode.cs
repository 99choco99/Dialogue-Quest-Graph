using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>다른 Quest의 현재 진행 단계에 따라 흐름을 분기합니다.</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/Condition/Quest State")]
    public sealed class QuestStateConditionNode : GraphNode<QuestStateConditionNodeData>
    {
        public override Vector2 DefaultSize => new(210f, 120f);

        /// <summary>입력 하나와 True, False 출력 포트를 만듭니다.</summary>
        protected override void Draw()
        {
            RefreshTitle();

            Port input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            input.portName = QuestPortNames.Input;
            inputContainer.Add(input);

            Port truePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            truePort.portName = QuestPortNames.True;
            outputContainer.Add(truePort);

            Port falsePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            falsePort.portName = QuestPortNames.False;
            outputContainer.Add(falsePort);

            AddToClassList("condition-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        private void RefreshTitle()
        {
            title = $"Q[{NodeData.QuestId}] == {NodeData.TargetState}";
        }

        /// <summary>Quest ID와 예상 상태 입력 요소를 만듭니다.</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new ();
            root.Add(new Label("Quest State Condition"));

            root.Add(QuestEditorFields.CreateQuestIdField(NodeData.QuestId, "Change inspected quest", editHandler,
                value =>
                {
                    NodeData.QuestId = value;
                    RefreshTitle();
                }));

            EnumField stateField = new ("Expected State", NodeData.TargetState);
            stateField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change expected quest state", () =>
                {
                    NodeData.TargetState = (QuestState)change.newValue;
                    RefreshTitle();
                });
            });
            root.Add(stateField);
            return root;
        }
    }
}
