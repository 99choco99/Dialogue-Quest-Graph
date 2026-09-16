using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>신호가 전달될 때까지 흐름을 멈춤</summary>
    [GraphNodeEditor(typeof(DialogueContainer), "Dialogue/Flow/Wait Signal")]
    public sealed class DialogueWaitSignalNode : GraphNode<DialogueWaitSignalNodeData>
    {
        public override Vector2 DefaultSize => new(220f, 120f);

        /// <summary>입력포트하나 출력포트 하나</summary>
        protected override void Draw()
        {
            RefreshPreview();
            Port input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            input.portName = "Input";
            inputContainer.Add(input);

            Port next = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            next.portName = DialoguePortNames.Next;
            outputContainer.Add(next);

            AddToClassList("wait-signal-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>신호 키로 노드 제목을 갱신</summary>
        private void RefreshPreview()
        {
            string key = NodeData?.SignalKey;
            title = string.IsNullOrWhiteSpace(key) ? "WAIT SIGNAL: 키 없음" : $"WAIT SIGNAL: {key}";
        }

        /// <summary>인스펙터에 신호키 하나 넣는 곳 만듬</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new();

            TextField signalKeyField = new("Signal Key")
            {
                value = NodeData.SignalKey,
                isDelayed = true
            };
            root.Add(signalKeyField);

            signalKeyField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change signal key", () =>
                {
                    NodeData.SignalKey = change.newValue;
                    signalKeyField.SetValueWithoutNotify(NodeData.SignalKey);
                    RefreshPreview();
                });
            });

            root.Add(new HelpBox("앞뒤 공백은 제거됩니다. Signal 키는 대소문자를 구분합니다.", HelpBoxMessageType.Info));
            return root;
        }
    }
}
