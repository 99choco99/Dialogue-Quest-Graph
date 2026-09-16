using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>Dialogue의 시작점을 의미하는 노드</summary>
    [GraphNodeEditor(typeof(DialogueContainer), "Dialogue/Entry")]
    public class DialogueEntryNode : GraphNode<DialogueEntryNodeData>
    {
        public override Vector2 DefaultSize => new(180f, 100f);

        /// <summary>노드가 처음 생성 됐을 때 데이터를 넣어주는 함수</summary>
        protected override void InitializeNewData(DialogueEntryNodeData data, GraphNodeCreationContext creationContext)
        {
            HashSet<string> usedEntryIds = new(creationContext.ExistingNodes.OfType<DialogueEntryNodeData>().Select(entry => entry.EntryId));

            if (!usedEntryIds.Contains(DialogueEntryNodeData.DefaultEntryId))
            {
                data.EntryId = DialogueEntryNodeData.DefaultEntryId;
                return;
            }

            int index = 2;
            string entryId;
            do
            {
                entryId = $"Entry{index++}";
            }
            while (usedEntryIds.Contains(entryId));

            data.EntryId = entryId;
        }

        /// <summary>노드 그리기. 출력 하나만 있음</summary>
        protected override void Draw()
        {
            capabilities &= ~Capabilities.Copiable;
            RefreshTitle();

            Port nextPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(float));
            nextPort.portName = DialoguePortNames.Next;
            outputContainer.Add(nextPort);

            AddToClassList("start-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>Entry ID가 바뀌면 GraphView에서 노드 제목을 갱신</summary>
        public void RefreshTitle()
        {
            title = $"ENTRY: {NodeData.EntryId}";
        }

        /// <summary>Entry Id 를 바꿀 수 있게 인스펙터에 편집 필드 생성</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new();
            root.Add(new Label("Dialogue를 사용하려면 이 그래프와 Entry ID를 전달해야 합니다."));

            TextField entryIdField = new("Entry ID")
            {
                value = NodeData.EntryId,
                isDelayed = true
            };
            root.Add(entryIdField);

            //경고문
            HelpBox duplicateWarning = new("Entry ID는 그래프 안에서 중복될 수 없습니다.", HelpBoxMessageType.Error);
            root.Add(duplicateWarning);
            UniversalGraphStyles.SetVisible(duplicateWarning, false);

            //field값 채울 시 발동되는 이벤트
            entryIdField.RegisterValueChangedCallback(change =>
            {
                string requestedEntryId = string.IsNullOrWhiteSpace(change.newValue) ? DialogueEntryNodeData.DefaultEntryId : change.newValue.Trim();
                UniversalGraphView graphView = GetFirstAncestorOfType<UniversalGraphView>();
                bool isDuplicate = graphView != null
                    && graphView.nodes.OfType<DialogueEntryNode>().Any(node => node != this && node.NodeData?.EntryId == requestedEntryId);

                if (isDuplicate)
                {
                    duplicateWarning.text = $"'{requestedEntryId}'은(는) 이미 사용 중입니다.";
                    UniversalGraphStyles.SetVisible(duplicateWarning, true);
                    entryIdField.SetValueWithoutNotify(NodeData.EntryId);
                    return;
                }

                editHandler.ApplyDataEdit("Change entry ID", () =>
                {
                    NodeData.EntryId = requestedEntryId;
                    entryIdField.SetValueWithoutNotify(NodeData.EntryId);
                    RefreshTitle();
                    UniversalGraphStyles.SetVisible(duplicateWarning, false);
                });
            });


            return root;
        }
    }
}
