using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>참조한 Quest가 지정 상태가 될 때까지 현재 흐름을 기다리는 노드</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/Flow/Wait For Quest")]
    public sealed class QuestStateWaitNode : QuestFlowNode<QuestStateWaitNodeData>
    {
        public override Vector2 DefaultSize => new(200f, 100f);

        /// <summary>대상 Quest ID와 기다릴 상태를 포함한 노드 제목</summary>
        protected override string NodeTitle => $"WAIT QUEST: {NodeData.TargetQuestId} ({NodeData.RequiredState})";

        /// <summary>기다릴 Quest ID와 상태 선택 필드를 만듭니다.</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new ();
            root.Add(new Label("Wait For Quest"));
            root.Add(new HelpBox("참조한 Quest가 지정한 상태가 되면 현재 흐름을 다시 진행합니다.", HelpBoxMessageType.Info));

            Button openButton = new()
            {
                text = "Open Target Quest Graph"
            };
            openButton.clicked += OpenReferencedQuest;

            //퀘스트 지정 필드(드롭다운)
            PopupField<int> questField = QuestEditorFields.CreateQuestIdField(NodeData.TargetQuestId, "Change target quest ID", editHandler,
                value =>
                {
                    NodeData.TargetQuestId = value;
                    RefreshTitle();
                    RefreshOpenButton();
                });
            root.Add(questField);

            //퀘스트 상태 요구 필드
            List<QuestState> allowedStates = new ()
            {
                QuestState.NotStarted,
                QuestState.InProgress,
                QuestState.CanComplete,
                QuestState.TurnedIn,
                QuestState.Failed
            };
            int selectedIndex = allowedStates.IndexOf(NodeData.RequiredState);
            PopupField<QuestState> stateField = new ("Required State", allowedStates, selectedIndex);
            stateField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change required quest state", () =>
                {
                    NodeData.RequiredState = change.newValue;
                    RefreshTitle();
                });
            });
            root.Add(stateField);

            //버튼 추가
            root.Add(openButton);
            RefreshOpenButton();
            return root;

            void RefreshOpenButton()
            {
                openButton.SetEnabled(QuestAssetCatalog.Containers.Count(container => container.QuestId == NodeData.TargetQuestId) == 1);
            }

            //버튼 누르면 참조 퀘스트 열림
            void OpenReferencedQuest()
            {
                QuestContainer[] targetContainers = QuestAssetCatalog.Containers.Where(container => container.QuestId == NodeData.TargetQuestId).ToArray();
                openButton.SetEnabled(targetContainers.Length == 1);
                if (targetContainers.Length == 1)
                {
                    UniversalGraphWindow.OpenWindow(targetContainers[0]);
                }
            }
        }
    }
}
