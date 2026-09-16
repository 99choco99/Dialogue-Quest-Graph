using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>등록된 조건을 평가한 뒤 Dialogue 흐름을 True or False로 분기</summary>
    [GraphNodeEditor(typeof(DialogueContainer), "Dialogue/Flow/Condition")]
    public sealed class DialogueConditionNode : GraphNode<DialogueConditionNodeData>
    {
        public override Vector2 DefaultSize => new(220f, 150f);

        /// <summary>입력 포트와 이름이 있는 True, False 출력 포트</summary>
        protected override void Draw()
        {
            RefreshPreview();

            Port input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            input.portName = "Input";
            inputContainer.Add(input);

            Port truePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            truePort.portName = DialoguePortNames.True;
            outputContainer.Add(truePort);

            Port falsePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            falsePort.portName = DialoguePortNames.False;
            outputContainer.Add(falsePort);

            AddToClassList("condition-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary> 조건이 업데이트 되면 갱신</summary>
        private void RefreshPreview()
        {
            title = NodeData?.Condition?.HasKey != true? "CONDITION: 선택 안 됨": $"CONDITION: {NodeData.Condition.Key}";
        }

        /// <summary>Condition 키 선택기와 자동 생성된 인수 입력 요소를 생성</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new ();
            root.Add(new HelpBox("조건 결과에 따라 True 또는 False 포트 중 하나로 진행합니다.", HelpBoxMessageType.Info));

            root.Add(MethodBindingInspector.Create(editHandler, "조건" , NodeData.Condition, DialogueMethodCatalog.GetMethodList(MethodKind.Condition), RefreshPreview));
            return root;
        }
    }
}
