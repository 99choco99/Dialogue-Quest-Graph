using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>Dialogue 그래프의 시작점을 제공하는 Quest 종착 노드</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/End/Dialogue Candidate")]
    public sealed class DialogueCandidateNode : GraphNode<DialogueCandidateNodeData>
    {
        public override Vector2 DefaultSize => new(250f, 150f);

        /// <summary>종착 노드의 입력 포트를 만듭니다.</summary>
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

        /// <summary>선택한 Dialogue 그래프와 시작점이 보이도록 갱신</summary>
        private void RefreshTitle()
        {
            string graphName = NodeData.EntryPoint.Container == null ? "None" : NodeData.EntryPoint.Container.name;
            title = $"Dialogue Candidate: {graphName} ({NodeData.EntryPoint.EntryId})";
        }

        /// <summary>Dialogue 시작점, 표시 이름, 우선순위 입력 칸 제작</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new ();
            root.Add(new HelpBox("이 종점에 도달하면 선택한 대화 진입점을 대화 선택기가 사용할 수 있습니다.", HelpBoxMessageType.Info));
            root.Add(QuestEditorFields.CreateDialogueEntryPointField(
                NodeData.EntryPoint,
                editHandler,
                entryPoint =>
                {
                    NodeData.EntryPoint = entryPoint;
                    RefreshTitle();
                }));

            //표시할 이름
            TextField displayNameField = new ("Display Name")
            {
                value = NodeData.DisplayName ?? string.Empty,
                isDelayed = true
            };
            displayNameField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change dialogue display name", () =>
                {
                    NodeData.DisplayName = change.newValue;
                });
            });
            root.Add(displayNameField);

            //우선순위
            IntegerField priorityField = new ("Priority")
            {
                value = NodeData.Priority,
                isDelayed = true
            };
            priorityField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change dialogue priority", () =>
                {
                    NodeData.Priority = change.newValue;
                });
            });
            root.Add(priorityField);

            return root;
        }
    }
}
