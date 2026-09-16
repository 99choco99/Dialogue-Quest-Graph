using System.Collections.Generic;
using System.Linq;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>Play Mode에 들어가기 전에 Quest 진행과 대화 경로 문제를 보고합니다.</summary>
    public sealed class QuestGraphValidator : GraphValidatorBase<QuestContainer>
    {
        /// <summary>Quest 흐름, 참조, 바인딩, 도달 가능 여부와 완료 경로를 검사합니다.</summary>
        protected override void Validate(
            QuestContainer container,
            GraphValidationIndex index,
            ICollection<GraphValidationIssue> issues)
        {
            ValidateQuestMetadata();

            if (index.Nodes.Count == 0)
            {
                AddError(
                    "QUEST_EMPTY_GRAPH",
                    "이 Quest에 그래프 흐름이 없습니다. 고정 ID를 지정하고 Quest Start 노드를 추가하세요.");
                return;
            }

            QuestStartNodeData[] starts = index.Nodes.OfType<QuestStartNodeData>().ToArray();
            QuestInteractionEntryNodeData[] interactionEntries = index.Nodes.OfType<QuestInteractionEntryNodeData>().ToArray();
            HashSet<string> progressionReachable = index.GetReachableNodeGuids(starts.Select(nodeData => nodeData.Guid));
            HashSet<string> interactionReachable = index.GetReachableNodeGuids(interactionEntries.Select(nodeData => nodeData.Guid));

            if (starts.Length != 1)
            {
                AddError(
                    "QUEST_START_COUNT",
                    $"Quest 그래프에는 Quest Start 노드가 정확히 하나 필요하지만 {starts.Length}개 발견되었습니다.");
            }

            foreach (NodeBaseData nodeData in index.Nodes)
            {
                bool inProgression = progressionReachable.Contains(nodeData.Guid);
                bool inInteraction = interactionReachable.Contains(nodeData.Guid);

                if (inInteraction && !IsInteractionRouteNode(nodeData))
                {
                    AddError(
                        "QUEST_ROUTE_UNSAFE_NODE",
                        $"'{nodeData.GetType().Name}'은 Quest 흐름을 변경하므로 상호작용 대화 경로에서 사용할 수 없습니다.",
                        nodeData.Guid);
                }

                switch (nodeData)
                {
                    case QuestStartNodeData startData:
                        RequireAtLeastOneOutput(startData.Guid, QuestPortNames.Next);
                        break;

                    case QuestInteractionEntryNodeData entryData:
                        RequireAtLeastOneOutput(entryData.Guid, QuestPortNames.Next);
                        break;

                    case QuestObjectiveNodeData objectiveData:
                        if (string.IsNullOrWhiteSpace(objectiveData.EventKey))
                        {
                            AddError("QUEST_OBJECTIVE_KEY", "이벤트 키가 필요합니다.", objectiveData.Guid);
                        }
                        if (objectiveData.RequiredAmount < 1)
                        {
                            AddError(
                                "QUEST_OBJECTIVE_AMOUNT",
                                "Objective Required Amount는 1 이상이어야 합니다.",
                                objectiveData.Guid);
                        }
                        if (inProgression)
                        {
                            RequireAtLeastOneOutput(objectiveData.Guid, QuestPortNames.Next);
                        }
                        break;

                    case QuestConditionNodeData conditionData:
                        ValidateMethodBinding(
                            conditionData.Guid,
                            MethodKind.Condition,
                            conditionData.Condition,
                            "Custom Condition",
                            required: true);
                        ValidateConditionOutputs(conditionData.Guid, inProgression);
                        break;

                    case QuestStateConditionNodeData stateConditionData:
                        ValidateQuestReference(stateConditionData.QuestId, "Quest 상태 조건", stateConditionData.Guid);
                        ValidateConditionOutputs(stateConditionData.Guid, inProgression);
                        break;

                    case QuestAndGateNodeData gateData:
                        int connectedSources = index.GetLinkInTargetPorts(gateData.Guid)
                            .Select(link => link.StartNodeGuid)
                            .Distinct()
                            .Count();
                        if (connectedSources < 2)
                        {
                            AddWarning(
                                "QUEST_REDUNDANT_AND",
                                $"AND Gate: 서로 다른 입력이 2개 미만입니다 (현재 {connectedSources}개).",
                                gateData.Guid);
                        }
                        if (inProgression)
                        {
                            RequireAtLeastOneOutput(gateData.Guid, QuestPortNames.Next);
                        }
                        break;

                    case QuestActionNodeData actionData:
                        ValidateMethodBinding(
                            actionData.Guid,
                            MethodKind.Action,
                            actionData.Action,
                            "Quest Action",
                            required: true);
                        if (inProgression)
                        {
                            RequireAtLeastOneOutput(actionData.Guid, QuestPortNames.Next);
                        }
                        break;

                    case QuestFlowEndNodeData flowEndData:
                        if (flowEndData.NewState != QuestState.CanComplete
                            && flowEndData.NewState != QuestState.TurnedIn
                            && flowEndData.NewState != QuestState.Failed)
                        {
                            AddError(
                                "QUEST_STATE_CHANGE_TARGET",
                                "State Change는 CanComplete, TurnedIn, Failed만 선택할 수 있습니다.",
                                flowEndData.Guid);
                        }

                        if (index.GetLinkInStartPort(flowEndData.Guid).Count > 0)
                        {
                            AddError(
                                "QUEST_TERMINAL_STATE_OUTPUT",
                                $"{flowEndData.NewState}은 현재 Quest 흐름을 끝내므로 나가는 연결선을 가질 수 없습니다.",
                                flowEndData.Guid);
                        }
                        break;

                    case QuestRewardNodeData rewardData:
                        ValidateMethodBinding(
                            rewardData.Guid,
                            MethodKind.Action,
                            rewardData.RewardAction,
                            "Reward Action");
                        if (inProgression)
                        {
                            RequireAtLeastOneOutput(rewardData.Guid, QuestPortNames.Next);
                        }
                        break;

                    case QuestStateWaitNodeData stateWaitData:
                        ValidateQuestReference(stateWaitData.TargetQuestId, "대기할 Quest", stateWaitData.Guid);
                        if (stateWaitData.RequiredState == QuestState.ExecutionError)
                        {
                            AddError("QUEST_WAIT_EXECUTION_ERROR", "실행 오류는 대기 조건으로 사용할 수 없습니다.", stateWaitData.Guid);
                        }
                        if (stateWaitData.TargetQuestId == container.QuestId)
                        {
                            AddWarning("QUEST_SELF_DEPENDENCY", "자기 Quest 대기: 지정한 상태에 도달할 수 있는지 확인하세요.", stateWaitData.Guid);
                        }
                        else if (inProgression)
                        {
                            ValidateWaitDependencyCycle(stateWaitData.TargetQuestId, stateWaitData.Guid);
                        }
                        if (inProgression)
                        {
                            RequireAtLeastOneOutput(stateWaitData.Guid, QuestPortNames.Next);
                        }
                        break;

                    case DialogueCandidateNodeData candidateData:
                        ValidateDialogueCandidate(candidateData);
                        if (inProgression)
                        {
                            AddError(
                                "QUEST_DIALOGUE_IN_PROGRESS_FLOW",
                                "Dialogue Candidate는 대화 경로의 종점이므로 Quest 진행을 앞으로 이동시킬 수 없습니다.",
                                candidateData.Guid);
                        }
                        break;

                    case QuestSuggestionNodeData suggestionNodeData:
                        ValidateQuestSuggestion(suggestionNodeData);
                        if (inProgression)
                        {
                            AddError(
                                "QUEST_OFFER_IN_PROGRESS_FLOW",
                                "Quest Suggestion은 상호작용 경로의 종점이므로 Quest 진행 흐름에서 사용할 수 없습니다.",
                                suggestionNodeData.Guid);
                        }
                        break;

                    default:
                        AddError(
                            "QUEST_UNSUPPORTED_NODE",
                            $"QuestManager가 노드 타입 '{nodeData.GetType().Name}'을 지원하지 않습니다.",
                            nodeData.Guid);
                        break;
                }
            }

            HashSet<string> reachable = new(progressionReachable);
            reachable.UnionWith(interactionReachable);
            if (progressionReachable.Count > 0)
            {
                foreach (NodeBaseData nodeData in index.Nodes.Where(nodeData => !reachable.Contains(nodeData.Guid)))
                {
                    AddWarning(
                        "QUEST_UNREACHABLE",
                        "Start 또는 Interaction Entry에서 도달할 수 없는 노드입니다.",
                        nodeData.Guid);
                }
            }

            foreach (string nodeGuid in index.FindCycleNodeGuids(_ => true))
            {
                AddError(
                    "QUEST_CYCLE",
                    "완료된 대기 노드는 다시 방문할 때 즉시 실행되므로 Quest 그래프에는 단방향 순환이 있을 수 없습니다.",
                    nodeGuid);
            }

            void ValidateQuestMetadata()
            {
                if (container.QuestId <= 0)
                {
                    AddError(
                        "QUEST_ID",
                        "양수인 고정 Quest ID를 지정하세요.");
                }

                QuestContainer[] duplicateContainers = QuestAssetCatalog.Containers
                    .Where(questContainer => questContainer != container && questContainer.QuestId == container.QuestId)
                    .ToArray();
                if (duplicateContainers.Length > 0)
                {
                    AddError(
                        "QUEST_DUPLICATE_ID",
                        $"Quest ID {container.QuestId}를 다음 에셋도 사용하고 있습니다: {string.Join(", ", duplicateContainers.Select(questContainer => questContainer.name))}.");
                }
            }

            void ValidateConditionOutputs(string nodeGuid, bool requiredForProgression)
            {
                if (!requiredForProgression)
                {
                    return;
                }

                foreach (string portName in new[] { QuestPortNames.True, QuestPortNames.False })
                {
                    int count = index.GetLinkInStartPort(nodeGuid, portName).Count;
                    if (count == 0)
                    {
                        AddError(
                            "QUEST_CONDITION_DEAD_END",
                            $"{portName}: 연결이 1개 이상 필요합니다 (현재 0개)",
                            nodeGuid);
                    }
                }
            }

            void ValidateQuestReference(int questId, string label, string nodeGuid)
            {
                if (questId <= 0 || !QuestAssetCatalog.Containers.Any(
                    questContainer => questContainer.QuestId == questId))
                {
                    AddError("QUEST_MISSING_REFERENCE", $"{label}이 존재하지 않는 Quest ID {questId}를 참조합니다.", nodeGuid);
                }
            }

            void ValidateWaitDependencyCycle(int targetQuestId, string nodeGuid)
            {
                if (!CanReachQuest(targetQuestId, container.QuestId, new HashSet<int>()))
                {
                    return;
                }

                AddWarning(
                    "QUEST_WAIT_DEPENDENCY_CYCLE",
                    $"Quest {container.QuestId} ↔ {targetQuestId}: 서로 기다리며 멈추지 않는지 확인하세요.",
                    nodeGuid);

                bool CanReachQuest(int currentQuestId, int destinationQuestId, ISet<int> visitedQuestIds)
                {
                    if (currentQuestId == destinationQuestId)
                    {
                        return true;
                    }

                    if (!visitedQuestIds.Add(currentQuestId))
                    {
                        return false;
                    }

                    QuestContainer targetContainer = QuestAssetCatalog.Containers.FirstOrDefault(
                        questContainer => questContainer.QuestId == currentQuestId);
                    if (targetContainer == null)
                    {
                        return false;
                    }

                    GraphValidationIssue structureError = GraphValidator.ValidateStructure(targetContainer)
                        .FirstOrDefault(issue => issue.Severity == GraphValidationSeverity.Error);
                    if (structureError != null)
                    {
                        AddError("QUEST_REFERENCE_STRUCTURE", $"Quest {currentQuestId}: {structureError.Message}", nodeGuid);
                        return false;
                    }

                    var targetIndex = new GraphValidationIndex(targetContainer);
                    IEnumerable<string> startGuids = targetIndex.Nodes
                        .OfType<QuestStartNodeData>()
                        .Select(startData => startData.Guid);
                    HashSet<string> reachableGuids = targetIndex.GetReachableNodeGuids(startGuids);
                    foreach (QuestStateWaitNodeData dependencyData in targetIndex.Nodes
                                 .OfType<QuestStateWaitNodeData>()
                                 .Where(stateWaitData => reachableGuids.Contains(stateWaitData.Guid)))
                    {
                        if (CanReachQuest(dependencyData.TargetQuestId, destinationQuestId, visitedQuestIds))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            void ValidateMethodBinding(
                string nodeGuid,
                MethodKind kind,
                MethodBindingData bindingData,
                string label,
                bool required = false)
            {
                if (bindingData == null)
                {
                    string issueKind = "QUEST_REWARD_DATA";
                    if (required)
                    {
                        issueKind = kind == MethodKind.Action ? "QUEST_ACTION_DATA" : "QUEST_CONDITION_DATA";
                    }
                    AddError(issueKind, $"{label} 호출 정보가 없습니다.", nodeGuid);
                    return;
                }

                if (!bindingData.HasKey)
                {
                    if (required)
                    {
                        string issueKind = kind == MethodKind.Action ? "QUEST_ACTION_KEY" : "QUEST_CONDITION_KEY";
                        AddError(issueKind, $"{label} Key가 필요합니다.", nodeGuid);
                    }
                    return;
                }

                if (!QuestMethodCatalog.GetMethodDescriptor(kind, bindingData.Key, out QuestMethodDescriptor descriptor))
                {
                    AddError(
                        "QUEST_METHOD_KEY",
                        $"등록되지 않은 Attribute {kind} 키 '{bindingData.Key}'입니다.",
                        nodeGuid);
                    return;
                }

                if (MethodArgumentCodec.TryDecodeAllArgumentData(bindingData.Arguments, descriptor, out _, out string error))
                {
                    return;
                }

                AddError(
                    "QUEST_METHOD_ARGUMENTS",
                    $"{label} '{bindingData.Key}': {error}",
                    nodeGuid);
            }

            void ValidateDialogueCandidate(DialogueCandidateNodeData candidateData)
            {
                ValidateDialogueEntryPoint(candidateData.EntryPoint, candidateData.Guid, "Dialogue Candidate");

                if (index.GetLinkInStartPort(candidateData.Guid).Count > 0)
                {
                    AddError(
                        "QUEST_DIALOGUE_OUTPUT",
                        "Dialogue Candidate는 조회 결과를 만드는 종점이므로 나가는 연결선을 가질 수 없습니다.",
                        candidateData.Guid);
                }
            }

            void ValidateQuestSuggestion(QuestSuggestionNodeData suggestionNodeData)
            {
                if (suggestionNodeData.DialogueEntryPoint.Container != null)
                {
                    ValidateDialogueEntryPoint(suggestionNodeData.DialogueEntryPoint, suggestionNodeData.Guid, "Quest Suggestion");
                }

                if (index.GetLinkInStartPort(suggestionNodeData.Guid).Count > 0)
                {
                    AddError(
                        "QUEST_OFFER_OUTPUT",
                        "Quest Suggestion은 조회 결과를 만드는 종점이므로 나가는 연결선을 가질 수 없습니다.",
                        suggestionNodeData.Guid);
                }
            }

            void ValidateDialogueEntryPoint(
                DialogueEntryPoint entryPoint,
                string nodeGuid,
                string label)
            {
                DialogueContainer graph = entryPoint.Container;
                if (graph == null)
                {
                    AddError("QUEST_DIALOGUE_GRAPH", $"{label}에 Dialogue Graph 에셋이 없습니다.", nodeGuid);
                    return;
                }

                GraphValidationIssue structureError = GraphValidator.ValidateStructure(graph)
                    .FirstOrDefault(issue => issue.Severity == GraphValidationSeverity.Error);
                if (structureError != null)
                {
                    AddError("QUEST_DIALOGUE_GRAPH", $"{label} '{graph.name}': {structureError.Message}", nodeGuid);
                    return;
                }

                if (!graph.FindEntryNode(
                        entryPoint.EntryId,
                        out DialogueEntryNodeData entryData,
                        out string error))
                {
                    AddError("QUEST_DIALOGUE_ENTRY", $"{label}: {error}", nodeGuid);
                    return;
                }

                int count = graph.NodeLinks.Count(link => link.StartNodeGuid == entryData.Guid && link.StartPortName == DialoguePortNames.Next);
                if (count != 1)
                {
                    AddError(
                        "QUEST_DIALOGUE_ENTRY",
                        $"{label} Entry '{entryData.EntryId}' Next: 연결 1개 필요 (현재 {count}개)",
                        nodeGuid);
                }
            }

            void RequireAtLeastOneOutput(string nodeGuid, string portName)
            {
                if (index.GetLinkInStartPort(nodeGuid, portName).Count == 0)
                {
                    AddError("QUEST_MISSING_OUTPUT", $"{portName}: 연결 필요", nodeGuid);
                }
            }

            void AddError(
                string issueKind,
                string message,
                string nodeGuid = null)
            {
                issues.Add(new GraphValidationIssue(
                    GraphValidationSeverity.Error,
                    issueKind,
                    message,
                    nodeGuid));
            }

            void AddWarning(string issueKind, string message, string nodeGuid = null)
            {
                issues.Add(new GraphValidationIssue(GraphValidationSeverity.Warning, issueKind, message, nodeGuid));
            }
        }

        private static bool IsInteractionRouteNode(NodeBaseData nodeData)
        {
            return nodeData is QuestInteractionEntryNodeData
                   || nodeData is QuestStateConditionNodeData
                   || nodeData is QuestConditionNodeData
                   || nodeData is DialogueCandidateNodeData
                   || nodeData is QuestSuggestionNodeData;
        }
    }
}
