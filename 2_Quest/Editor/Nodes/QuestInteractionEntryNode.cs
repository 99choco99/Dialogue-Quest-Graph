using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>상호작용 대상에 제공할 대화 후보와 퀘스트 제안을 조회하는 시작 노드</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/Start/InteractionEntry")]
    public sealed class QuestInteractionEntryNode : GraphNode<QuestInteractionEntryNodeData>
    {
        public override Vector2 DefaultSize => new(190f, 100f);

        /// <summary>출력 포트 생성</summary>
        protected override void Draw()
        {
            RefreshTitle();

            Port next = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            next.portName = QuestPortNames.Next;
            outputContainer.Add(next);

            AddToClassList("quest-entry-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        private void RefreshTitle()
        {
            title = $"INTERACT: {(string.IsNullOrWhiteSpace(NodeData.TargetId) ? "Any" : NodeData.TargetId)}";
        }

        /// <summary>프로젝트에서 정의하는 상호작용 대상 필드를 만듭니다.</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new ();
            root.Add(new Label("Interaction Entry"));
            root.Add(new HelpBox(
                "상호작용 대상에게 제공할 대화 또는 Quest 선택 항목을 찾는 진입점입니다. " +
                "모든 대상과 일치시키려면 Interaction Target ID를 비워 두세요.", HelpBoxMessageType.Info));

            TextField interactionTargetField = new ("어느 interaction 에 반응할건지")
            {
                value = NodeData.TargetId,
                isDelayed = true
            };

            interactionTargetField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change interaction target", () =>
                {
                    NodeData.TargetId = change.newValue;
                    interactionTargetField.SetValueWithoutNotify(NodeData.TargetId);
                    RefreshTitle();
                });
            });

            root.Add(interactionTargetField);
            return root;
        }
    }
}
