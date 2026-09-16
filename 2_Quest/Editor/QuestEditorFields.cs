using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>실수하기 쉬운 숫자 직접 입력을 대신하는 재사용 가능한 Quest 참조 필드입니다.</summary>
    internal static class QuestEditorFields
    {
        /// <summary>퀘스트 그래프에서 DialogueEntryPoint를 사용해야 할 때 참조시켜줄 필드를 생성하는 함수</summary>
        public static VisualElement CreateDialogueEntryPointField(DialogueEntryPoint currentEntryPoint, NodeInspectorEditHandler editHandler, Action<DialogueEntryPoint> apply)
        {
            VisualElement root = new ();
            DialogueEntryPoint entryPoint = currentEntryPoint;

            //Dialogue Graph 넣을 곳
            ObjectField graphField = new ("Graph Asset")
            {
                objectType = typeof(DialogueContainer),
                allowSceneObjects = false,
                value = entryPoint.Container
            };

            //EntryId 설정하는 곳
            PopupField<string> entryField = new ("Entry ID")
            {
                choices = GetDialogueEntryList(entryPoint.Container)
            };
            entryField.formatSelectedValueCallback = id =>
            {
                if (entryField.choices.Count == 0)
                    return "선택 가능한 Entry 없음";

                if (entryField.choices.Contains(id))
                    return id;

                return $"<다시 선택 필요> {id}";
            };
            entryField.SetValueWithoutNotify(entryPoint.EntryId);
            entryField.SetEnabled(entryField.choices.Count > 0);

            //설정한 그래프 조회하는 버튼
            Button openGraphButton = new(() =>
            {
                if (entryPoint.Container != null)
                {
                    UniversalGraphWindow.OpenWindow(entryPoint.Container);
                }
            }) { text = "Open Dialogue Graph" };
            openGraphButton.SetEnabled(entryPoint.Container != null);

            graphField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change dialogue graph", () =>
                {
                    entryPoint.Container = change.newValue as DialogueContainer;
                    apply(entryPoint);

                    entryField.choices = GetDialogueEntryList(entryPoint.Container);
                    entryField.SetValueWithoutNotify(entryPoint.EntryId);
                    entryField.SetEnabled(entryField.choices.Count > 0);
                    openGraphButton.SetEnabled(entryPoint.Container != null);
                });
            });


            entryField.RegisterValueChangedCallback(change =>
            {
                editHandler.ApplyDataEdit("Change dialogue entry", () =>
                {
                    entryPoint.EntryId = change.newValue;
                    apply(entryPoint);
                });
            });

            root.Add(graphField);
            root.Add(entryField);
            root.Add(openGraphButton);
            return root;
        }

        /// <summary>프로젝트 에셋 기반 Quest 선택기를 만들며 누락된 ID는 복구할 수 있도록 유지</summary>
        public static PopupField<int> CreateQuestIdField(int currentQuestId, string undoName, NodeInspectorEditHandler editHandler, Action<int> apply)
        {
            List<int> ids = QuestAssetCatalog.Containers
                .Select(container => container.QuestId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            PopupField<int> field = new ("TargetQuest")
            {
                choices = ids,
                formatSelectedValueCallback = id => ids.Count == 0 ? "선택 가능한 Quest 없음" : FormatQuest(id),
                formatListItemCallback = FormatQuest
            };
            field.SetValueWithoutNotify(currentQuestId);
            field.SetEnabled(ids.Count > 0);
            field.RegisterValueChangedCallback(change => editHandler.ApplyDataEdit(undoName, () => apply(change.newValue)));
            return field;
        }

        /// <summary>
        /// DialogueEntry 의 후보군을 리스트로 반환
        /// </summary>
        private static List<string> GetDialogueEntryList(DialogueContainer container)
        {
            return container?.Nodes?
                .OfType<DialogueEntryNodeData>()
                .Select(entryData => entryData.EntryId)
                .Distinct()
                .OrderByDescending(id => id == DialogueEntryNodeData.DefaultEntryId)
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToList() ?? new();

        }

        /// <summary>
        /// 드롭다운에 띄울 형식
        /// </summary>
        private static string FormatQuest(int questId)
        {
            QuestContainer[] matchingContainers = QuestAssetCatalog.Containers
                .Where(container => container.QuestId == questId)
                .ToArray();
            return matchingContainers.Length switch
            {
                0 => $"<존재하지 않음> {questId}",
                1 => $"{questId} - {matchingContainers[0].questName}",
                _ => $"<Duplicate> {questId} ({matchingContainers.Length} assets)"
            };
        }
    }
}
