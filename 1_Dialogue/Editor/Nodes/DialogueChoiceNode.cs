using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>선택지들을 띄우는 노드</summary>
    [GraphNodeEditor(typeof(DialogueContainer), "Dialogue/Choice")]
    public sealed class DialogueChoiceNode : GraphNode<DialogueChoiceNodeData>
    {
        public override Vector2 DefaultSize => new(220f, 200f);

        /// <summary>흐름 입력, Default 출력과 선택지별 출력 포트를 만들기</summary>
        protected override void Draw()
        {
            Port input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            input.portName = "Input";
            inputContainer.Add(input);

            Port defaultPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            defaultPort.portName = DialoguePortNames.Default;
            outputContainer.Add(defaultPort);

            foreach (DialogueChoiceData choice in NodeData.Choices)
            {
                AddChoicePort(choice);
            }

            RefreshPreview();
            AddToClassList("choice-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>현재 선택지 개수로 노드 제목을 갱신</summary>
        public void RefreshPreview()
        {
            title = $"CHOICE: {NodeData?.Choices?.Count ?? 0}";
            foreach (Port port in outputContainer.Children().OfType<Port>())
            {
                if (port.userData is not DialogueChoiceData choice)
                    continue;

                Label label = port.Q<Label>("choice-text");
                if (label != null)
                    label.text = choice.ChoiceText;
            }
        }

        /// <summary>선택지 데이터와 포트를 함께 추가하고 노드 표시를 갱신</summary>
        public void AddChoice(DialogueChoiceData choiceData)
        {
            AddChoicePort(choiceData);
            NodeData.Choices.Add(choiceData);
            RefreshPreview();
            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>선택지 포트를 하나 추가하는 함수</summary>
        private void AddChoicePort(DialogueChoiceData choiceData)
        {
            if (choiceData == null || string.IsNullOrWhiteSpace(choiceData.PortId))
            {
                throw new ArgumentException("선택지 데이터에는 고정된 포트 이름이 필요합니다.", nameof(choiceData));
            }

            Port port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            port.portName = choiceData.PortId;
            port.userData = choiceData;

            //포트의 이름을 변경하는 이상한 방법
            Label typeLabel = port.contentContainer.Q<Label>("type");
            if (typeLabel != null)
            {
                UniversalGraphStyles.SetVisible(typeLabel, false);
                port.contentContainer.Add(new Label(choiceData.ChoiceText)
                {
                    name = "choice-text"
                });
            }

            outputContainer.Add(port);
        }

        /// <summary>선택지 데이터와 포트, 연결선을 함께 삭제하고 노드 표시를 갱신</summary>
        public void RemoveChoice(DialogueChoiceData choiceData)
        {
            Port port = outputContainer.Children().OfType<Port>()
                .FirstOrDefault(candidate => ReferenceEquals(candidate.userData, choiceData) || candidate.portName == choiceData.PortId) 
                ?? throw new InvalidOperationException($"선택지 '{choiceData.PortId}'에 연결된 출력 포트를 찾지 못했습니다.");

            GraphView graphView = GetFirstAncestorOfType<GraphView>();
            foreach (Edge edge in port.connections.ToList())
            {
                edge.input?.Disconnect(edge);
                edge.output?.Disconnect(edge);
                graphView?.RemoveElement(edge);
            }

            port.RemoveFromHierarchy();
            NodeData.Choices.Remove(choiceData);

            RefreshPreview();
            RefreshPorts();
            RefreshExpandedState();
            MarkDirtyRepaint();
        }

        /// <summary>선택지 목록을 인스펙터에 그리기</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            return DialogueChoiceNodeInspectorDrawer.Draw(this, editHandler);
        }



        /// <summary>고정된 출력 포트를 만들 수 없는 선택지 데이터를 거부</summary>
        protected override void ValidateDataForView(DialogueChoiceNodeData data)
        {
            if (data.Choices == null)
            {
                throw new InvalidOperationException("선택지 목록이 null입니다.");
            }

            HashSet<string> portIds = new() { DialoguePortNames.Default };
            foreach (DialogueChoiceData choice in data.Choices)
            {
                if (choice == null)
                {
                    throw new InvalidOperationException("선택지 목록에 null 항목이 있습니다.");
                }

                if (string.IsNullOrWhiteSpace(choice.PortId))
                {
                    throw new InvalidOperationException("선택지에 포트 ID가 없습니다.");
                }

                if (!portIds.Add(choice.PortId))
                {
                    throw new InvalidOperationException($"선택지 출력 포트 ID '{choice.PortId}'가 중복되었습니다.");
                }
            }
        }
    }
}
