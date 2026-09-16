using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>Dialogue를 마무리 짓는 노드</summary>
    [GraphNodeEditor(typeof(DialogueContainer), "Dialogue/End")]
    public sealed class DialogueEndNode : GraphNode<DialogueEndNodeData>
    {
        public override Vector2 DefaultSize => new(140f, 100f);

        /// <summary>종료 흐름을 받는 노드를 그리기</summary>
        protected override void Draw()
        {
            title = "END";

            Port input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            input.portName = "Input";
            inputContainer.Add(input);

            AddToClassList("end-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>인스펙터에 종료 동작을 설명</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new();
            root.Add(new HelpBox("이 노드에 도달하면 대화가 완료됩니다. 출력 포트는 없습니다.", HelpBoxMessageType.Info));
            return root;
        }
    }
}
