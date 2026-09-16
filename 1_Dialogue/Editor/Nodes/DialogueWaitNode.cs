using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>설정한 시간 동안 현재 Dialogue 흐름을 멈춤</summary>
    [GraphNodeEditor(typeof(DialogueContainer), "Dialogue/Flow/Wait")]
    public sealed class DialogueWaitNode : GraphNode<DialogueWaitNodeData>
    {
        public override Vector2 DefaultSize => new(190f, 120f);

        /// <summary>흐름 입력 포트와 다음 흐름 출력 포트를 만듭니다.</summary>
        protected override void Draw()
        {
            RefreshPreview();
            Port input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            input.portName = "Input";
            inputContainer.Add(input);

            Port next = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            next.portName = DialoguePortNames.Next;
            outputContainer.Add(next);

            AddToClassList("wait-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>현재 대기 시간과 사용할 시간 기준으로 갱신</summary>
        private void RefreshPreview()
        {
            string timeSource = NodeData != null && NodeData.UseUnscaledTime ? "Unscaled" : "Scaled";
            float duration = NodeData?.DurationSeconds ?? 0f;
            title = $"WAIT: {duration:0.###}s ({timeSource})";
        }

        /// <summary>인스펙터에 대기 시간과 시간 배율 사용 여부를 넣기</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new();

            FloatField durationField = new("Duration (Seconds)")
            {
                value = NodeData.DurationSeconds,
                isDelayed = true
            };

            durationField.RegisterValueChangedCallback(change =>
            {
                float duration = change.newValue;
                if (float.IsNaN(duration) || float.IsInfinity(duration))
                {
                    durationField.SetValueWithoutNotify(NodeData.DurationSeconds);
                    return;
                }

                duration = Mathf.Max(0f, duration);
                editHandler.ApplyDataEdit("Change wait duration", () =>
                {
                    NodeData.DurationSeconds = duration;
                    durationField.SetValueWithoutNotify(duration);
                    RefreshPreview();
                });
            });
            root.Add(durationField);

            Toggle useUnscaledTimeField = new("Use Unscaled Time")
            {
                value = NodeData.UseUnscaledTime
            };

            useUnscaledTimeField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change wait time source", () =>
                {
                    NodeData.UseUnscaledTime = change.newValue;
                    RefreshPreview();
                });
            });

            root.Add(useUnscaledTimeField);
            root.Add(new HelpBox("Unscaled Time은 Time.timeScale이 0이어도 계속 흐릅니다.", HelpBoxMessageType.Info));
            return root;
        }
    }
}
