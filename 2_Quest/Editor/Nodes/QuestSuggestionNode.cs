using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>퀘스트를 제안하고 수락 여부를 물어볼 수 있도록 정보를 제공하는 노드.</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/End/Quest Suggestion")]
    public sealed class QuestSuggestionNode : GraphNode<QuestSuggestionNodeData>
    {
        public override Vector2 DefaultSize => new(250f, 180f);

        /// <summary>여러 조건 경로가 도달할 수 있는 입력 포트 하나를 만듭니다.</summary>
        protected override void Draw()
        {
            RefreshTitle();

            Port input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            input.portName = QuestPortNames.Input;
            inputContainer.Add(input);

            AddToClassList("end-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>선택 항목 상태, 차단 이유, 선택적인 대화와 우선순위 입력 요소를 만듭니다.</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new ();
            root.Add(new HelpBox("이 종점에 도달하면 현재 Quest가 상호작용 목록에 표시됩니다. " + 
                "수락할 수 없는 경로도 표시하려면 Is Available을 끄고 이유를 작성하세요.", HelpBoxMessageType.Info));

            //차단 이유
            TextField reasonField = new ("Block Reason")
            {
                value = NodeData.BlockReason ?? string.Empty,
                multiline = true,
                isDelayed = true
            };
            reasonField.SetEnabled(!NodeData.IsAvailable);

            reasonField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change quest suggestion block reason", () =>
                {
                    NodeData.BlockReason = change.newValue;
                });
            });

            //차단 여부
            Toggle availableField = new("Is Available")
            {
                value = NodeData.IsAvailable
            };
            availableField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change quest suggestion availability", () =>
                {
                    NodeData.IsAvailable = change.newValue;
                    reasonField.SetEnabled(!change.newValue);
                    RefreshTitle();
                });
            });

            root.Add(availableField);
            root.Add(reasonField);

            //우선순위
            IntegerField priorityField = new("우선순위")
            {
                value = NodeData.Priority,
                isDelayed = true
            };
            priorityField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change quest suggestion priority", () =>
                {
                    NodeData.Priority = change.newValue;
                });
            });
            root.Add(priorityField);

            //대화문 참조
            root.Add(QuestEditorFields.CreateDialogueEntryPointField(NodeData.DialogueEntryPoint, editHandler,
                entryPoint =>
                {
                    NodeData.DialogueEntryPoint = entryPoint;
                }));

            return root;
        }

        private void RefreshTitle()
        {
            title = NodeData.IsAvailable ? "QUEST SUGGESTION" : "QUEST SUGGESTION: BLOCKED";
        }
    }
}
