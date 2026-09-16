using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>등록된 Dialogue Action 하나를 실행하고 Next 포트로 진행</summary>
    [GraphNodeEditor(typeof(DialogueContainer), "Dialogue/Flow/Action")]
    public sealed class DialogueActionNode : GraphNode<DialogueActionNodeData>
    {
        public override Vector2 DefaultSize => new(220f, 150f);

        /// <summary>입력 하나 , 출력 하나</summary>
        protected override void Draw()
        {
            RefreshPreview();
            Port input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            input.portName = "Input";
            inputContainer.Add(input);

            Port next = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            next.portName = DialoguePortNames.Next;
            outputContainer.Add(next);

            AddToClassList("action-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>현재 연결된 Action 키로 노드 제목을 갱신</summary>
        private void RefreshPreview()
        {
            title = NodeData?.Action?.HasKey != true ? "ACTION: 선택 안 됨" : $"ACTION: {NodeData.Action.Key}";
        }

        /// <summary>인스펙터에 메서드 하나를 선택하고 그 파라미터들 값들 채울 수 있게 생성</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new();

            root.Add(new HelpBox("Action을 한 번 실행한 뒤 Next 포트로 진행합니다.", HelpBoxMessageType.Info));
            root.Add(MethodBindingInspector.Create(editHandler, "Action", NodeData.Action, DialogueMethodCatalog.GetMethodList(MethodKind.Action), RefreshPreview));
            return root;
        }
    }
}
