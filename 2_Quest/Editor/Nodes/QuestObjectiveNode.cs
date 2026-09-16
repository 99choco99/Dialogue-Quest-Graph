using System;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>일치하는 게임 이벤트가 목표량에 도달할 때까지 기다리는 노드</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/Flow/Objective")]
    public sealed class QuestObjectiveNode : QuestFlowNode<QuestObjectiveNodeData>
    {
        protected override string NodeTitle =>
            $"OBJECTIVE: {(string.IsNullOrWhiteSpace(NodeData.EventKey) ? "Unassigned" : NodeData.EventKey)} " +
            $"x{NodeData.RequiredAmount}";

        /// <summary>목표 이벤트, 대상, 수량, 참조와 설명 필드를 제작</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new ();
            root.Add(new Label("Objective"));

            //반응할 이벤트 키
            TextField eventKeyField = new ("Event Key")
            {
                value = NodeData.EventKey,
                isDelayed = true
            };
            eventKeyField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change objective event key", () =>
                {
                    NodeData.EventKey = change.newValue;
                    eventKeyField.SetValueWithoutNotify(NodeData.EventKey);
                    RefreshTitle();
                });
            });
            root.Add(eventKeyField);


            //Objective의 목표 타겟 id
            IntegerField objectiveTargetField = new ("Objective Target ID")
            {
                value = NodeData.TargetId,
                isDelayed = true
            };
            objectiveTargetField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change objective target", () =>
                {
                    NodeData.TargetId = change.newValue;
                });
            });
            root.Add(objectiveTargetField);

            //Ui등에서 Target Id의 실물을 활용하기 편하게 제공
            ObjectField targetReferenceField = new ("Authoring Reference")
            {
                objectType = typeof(UnityEngine.Object),
                allowSceneObjects = false,
                value = NodeData.TargetReference,
                tooltip = "이벤트 일치 여부는 Objective Target ID(TargetId)로 판단합니다."
            };
            targetReferenceField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change objective reference", () =>
                {
                    NodeData.TargetReference = change.newValue;
                });
            });
            root.Add(targetReferenceField);

            //필요 수량
            IntegerField required = new ("Required Amount")
            {
                value = NodeData.RequiredAmount,
                isDelayed = true
            };
            required.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change objective amount", () =>
                {
                    NodeData.RequiredAmount = Math.Max(1, change.newValue);
                    required.SetValueWithoutNotify(NodeData.RequiredAmount);
                    RefreshTitle();
                });
            });
            root.Add(required);

            //목표 설명
            TextField description = new ("Description")
            {
                value = NodeData.ObjectiveDescription ?? string.Empty,
                multiline = true,
                isDelayed = true
            };
            description.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change objective description", () =>
                {
                    NodeData.ObjectiveDescription = change.newValue;
                });
            });
            root.Add(description);

            return root;
        }

    }
}
