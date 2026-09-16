using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>
    /// QuestIneteractionEntry에서 시작해서 정보를 긁어모으는 클래스
    /// </summary>
    internal static class QuestInteractionQuery
    {
        /// <summary>
        /// ID 와 일치하는 모든 유효한 대화 후보를 반환
        /// <para>비어 있는 ID는 모든 대상과 일치</para>
        /// </summary>
        internal static List<DialogueCandidate> GetDialogueCandidates(QuestContainerRegistry registry, IQuestController controller, IEnumerable<string> interactionTargetIds)
        {
            List<DialogueCandidate> dialogueCandidates = new ();
            CollectCandidates(registry, controller, interactionTargetIds, dialogueCandidates, null);
            return dialogueCandidates;
        }

        /// <summary>모든 Quest 선택 항목을 반환</summary>
        internal static List<QuestSuggestion> GetQuestSuggestions(QuestContainerRegistry registry, IQuestController controller, IEnumerable<string> interactionTargetIds)
        {
            List<QuestSuggestion> questSuggestions = new ();
            CollectCandidates(registry, controller, interactionTargetIds, null, questSuggestions);
            return questSuggestions;
        }

        //========================================= 내부 처리 함수 ==========================================

        /// <summary>
        /// 대상 ID와 일치하는 Interaction Entry를 찾아 전달받은 후보 목록에 결과를 추가
        /// </summary>
        private static void CollectCandidates(QuestContainerRegistry registry, IQuestController controller, IEnumerable<string> interactionTargetIds, ICollection<DialogueCandidate> dialogueCandidates, ICollection<QuestSuggestion> questSuggestions)
        {
            HashSet<string> interactionTargetIdsSet = new (interactionTargetIds?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()) ?? Enumerable.Empty<string>());

            foreach (QuestContainer container in registry.Containers)
            {
                QuestGraphIndex index = registry.GetQuestGraphIndex(container.QuestId);

                foreach (QuestInteractionEntryNodeData entryData in container.Nodes.OfType<QuestInteractionEntryNodeData>())
                {
                    string targetId = entryData.TargetId;
                    if (targetId.Length == 0 || interactionTargetIdsSet.Contains(targetId))
                    {
                        CollectCandidatesFromEntry(container, controller, index, entryData, dialogueCandidates, questSuggestions);
                    }
                }
            }
        }


        /// <summary>
        /// Interaction Entry 부터 시작해서 bfs로 노드 탐색하며 처리
        /// </summary>
        private static void CollectCandidatesFromEntry(
            QuestContainer container,
            IQuestController controller,
            QuestGraphIndex index,
            QuestInteractionEntryNodeData entryData,
            ICollection<DialogueCandidate> dialogueCandidates,
            ICollection<QuestSuggestion> questSuggestions)
        {

            Queue<NodeBaseData> queue = new ();
            HashSet<string> visited = new();
            EnqueueNextNodes(index, queue, entryData.Guid, QuestPortNames.Next);

            while (queue.Count > 0)
            {
                NodeBaseData nodeData = queue.Dequeue();
                if (!visited.Add(nodeData.Guid))
                {
                    continue;
                }

                bool result;
                switch (nodeData)
                {
                    case DialogueCandidateNodeData dialogueCandidateData:
                        if (dialogueCandidateData.EntryPoint.Container != null)
                        {
                            dialogueCandidates?.Add(new DialogueCandidate(
                                dialogueCandidateData.EntryPoint,
                                dialogueCandidateData.DisplayName,
                                dialogueCandidateData.Priority));
                        }
                        continue;

                    case QuestSuggestionNodeData suggestionNodeData:
                        controller.QuestProgress.TryGetValue(container.QuestId, out QuestProgress suggestionProgress);
                        bool canStart = (suggestionProgress?.state ?? QuestState.NotStarted) == QuestState.NotStarted;
                        questSuggestions?.Add(new QuestSuggestion(
                            container,
                            suggestionNodeData.DialogueEntryPoint,
                            suggestionNodeData.Priority,
                            suggestionNodeData.IsAvailable && canStart,
                            suggestionNodeData.BlockReason,
                            entryData.Guid,
                            suggestionNodeData.Guid));
                        continue;

                    case QuestStateConditionNodeData stateConditionData:
                        controller.QuestProgress.TryGetValue(stateConditionData.QuestId, out QuestProgress targetProgress);
                        QuestState state = targetProgress?.state ?? QuestState.NotStarted;
                        result = state == stateConditionData.TargetState;
                        break;

                    case QuestConditionNodeData conditionData:
                        controller.QuestProgress.TryGetValue(container.QuestId, out QuestProgress currentProgress);
                        QuestExecutionContext context = new (controller, container, currentProgress, conditionData);
                        if (!QuestMethodInvoker.InvokeMethod(conditionData.Condition, context, MethodKind.Condition, out result))
                        {
                            continue;
                        }
                        break;

                    default:
                        Debug.LogWarning($"[Quest Interaction] 노드 타입 '{nodeData.GetType().Name}'은 조회 전용 경로에서 안전하지 않아 탐색을 종료합니다.", container);
                        continue;
                }

                EnqueueNextNodes(index, queue, nodeData.Guid, result ? QuestPortNames.True : QuestPortNames.False);
            }
        }

        /// <summary>
        /// 다음 노드를 queue에 넣기
        /// </summary>
        private static void EnqueueNextNodes(QuestGraphIndex index, Queue<NodeBaseData> queue, string guid, string port)
        {
            if (!index.OutputLinksByStartNode.TryGetValue(guid, out List<NodeLinkData> outputLinks))
            {
                return;
            }

            foreach (NodeLinkData link in outputLinks)
            {
                if (link.StartPortName == port)
                {
                    queue.Enqueue(index.Nodes[link.TargetNodeGuid]);
                }
            }
        }


        /// <summary>선택 항목을 만든 같은 시작점부터 조건을 다시 평가하고 현재 결과를 반환합니다.</summary>
        internal static bool RefreshQuestSuggestion(QuestContainerRegistry registry, IQuestController controller, QuestSuggestion suggestion, out QuestSuggestion refreshedSuggestion)
        {
            refreshedSuggestion = null;

            if (!registry.GetQuestGraphIndex(suggestion.QuestId, out QuestContainer container, out QuestGraphIndex index)
                || container != suggestion.Container
                || !index.Nodes.TryGetValue(suggestion.InteractionEntryGuid, out NodeBaseData nodeData)
                || nodeData is not QuestInteractionEntryNodeData entryData)
            {
                return false;
            }

            List<QuestSuggestion> questSuggestions = new();
            CollectCandidatesFromEntry(container, controller, index, entryData, null, questSuggestions);
            refreshedSuggestion = questSuggestions.FirstOrDefault(current => current.SuggestionNodeGuid == suggestion.SuggestionNodeGuid);
            return refreshedSuggestion != null;
        }

    }
}
