using System.Collections.Generic;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>현재 Quest를 완료 보고 대기, 완료 또는 실패 상태로 바꾸고 그래프 진행을 끝냅니다.</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/Flow/Change State")]
    public sealed class QuestFlowEndNode : QuestFlowNode<QuestFlowEndNodeData>
    {
        protected override string NodeTitle => $"STATE: {NodeData.NewState}";
        protected override bool HasOutput => false;

        /// <summary>종료할 상태를 선택합니다. 세 상태 모두 종점이므로 연결 구조는 바뀌지 않습니다.</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            List<QuestState> allowedStates = new ()
            {
                QuestState.CanComplete,
                QuestState.TurnedIn,
                QuestState.Failed
            };

            int selectedIndex = allowedStates.IndexOf(NodeData.NewState);
            PopupField<QuestState> field = new ("New State", allowedStates, selectedIndex);
            field.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change quest state", () =>
                {
                    NodeData.NewState = change.newValue;
                    RefreshTitle();
                });
            });
            return field;
        }
    }
}
