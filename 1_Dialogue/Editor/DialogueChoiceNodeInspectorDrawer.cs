using System;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>Choice 노드의 선택지 목록을 인스펙터에 그리는 클래스</summary>
    internal static class DialogueChoiceNodeInspectorDrawer
    {
        /// <summary>선택한 Choice 노드의 인스펙터를 생성</summary>
        public static VisualElement Draw(DialogueChoiceNode selectedNode, NodeInspectorEditHandler editHandler)
        {
            VisualElement root = new();

            root.Add(new HelpBox("표시 가능한 선택지가 없으면 Default 포트로 즉시 진행합니다.", HelpBoxMessageType.Info));

            //선택지 추가 버튼과 선택지들이 들어갈 공간을 생성
            VisualElement section = new();
            root.Add(section);
            section.Add(new Label("Choices"));

            VisualElement choicesContainer = new();
            section.Add(choicesContainer);

            //동적으로 생성하는 버튼
            Button addButton = new(() =>
            {
                editHandler.ApplyStructureEdit("Add dialogue choice", () =>
                {
                    DialogueChoiceData choice = new()
                    {
                        ChoiceText = "New Choice"
                    };
                    selectedNode.AddChoice(choice);
                    RedrawChoices();
                });
            })
            {
                text = "+ Add Choice"
            };
            section.Add(addButton);

            RedrawChoices();
            return root;

            //내부함수. 선택지 생성 후 다시 인스펙터에서 선택지UI 를 그려야 하기 때문
            void RedrawChoices()
            {
                choicesContainer.Clear();
                foreach (DialogueChoiceData choice in selectedNode.NodeData.Choices)
                {
                    if (choice != null)
                    {
                        choicesContainer.Add(CreateChoiceField(selectedNode, choice, editHandler, RedrawChoices));
                    }
                }
            }
        }

        /// <summary>인스펙터에 선택지 하나에 대한 정보를 넣을 수 있는 필드 생성</summary>
        private static Box CreateChoiceField(DialogueChoiceNode selectedNode, DialogueChoiceData choice, NodeInspectorEditHandler editHandler, Action redrawChoices)
        {
            Box box = new();

            //삭제 버튼 추가
            Button deleteButton = new(() =>
            {
                editHandler.ApplyStructureEdit("Delete dialogue choice", () =>
                {
                    selectedNode.RemoveChoice(choice);
                    redrawChoices?.Invoke();
                });
            })
            {
                text = "×",
                tooltip = "선택지 삭제"
            };
            deleteButton.AddToClassList("choice-delete-btn");
            box.Add(deleteButton);

            //텍스트 영역
            TextField textField = new("Text")
            {
                value = choice.ChoiceText ?? string.Empty,
                multiline = true
            };
            textField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change choice text", () => {
                    choice.ChoiceText = change.newValue;
                    selectedNode.RefreshPreview();
                });
            });
            box.Add(textField);

            //선택지 공개 조건
            box.Add(MethodBindingInspector.Create(editHandler, "선택지 공개 조건", choice.VisibilityCondition, DialogueMethodCatalog.GetMethodList(MethodKind.Condition)));

            return box;
        }
    }
}
