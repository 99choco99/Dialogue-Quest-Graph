using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>Quest 노드 대기열과 즉시 실행 흐름을 담당합니다.</summary>
    public static partial class QuestManager
    {
        private const int MaxImmediateNodeSteps = 256;

        /// <summary>
        /// 현재 실행중인 게임 이벤트 노드의 임시 목록. 중복 실행을 막는 용도
        /// </summary>
        private static readonly HashSet<(QuestProgress Progress, int RunVersion, string NodeGuid)> executingActionNodes = new();

        /// <summary>등록부를 별도로 보관하지 않고 현재 Instance로 접근합니다.</summary>
        private static QuestContainerRegistry Registry => QuestContainerRegistry.Instance;

        private readonly struct FlowStep
        {
            public FlowStep(NodeBaseData nodeData, string preNodeGuid)
            {
                CurrentNodeData = nodeData;
                PreNodeGuid = preNodeGuid;
            }
            public NodeBaseData CurrentNodeData { get; }
            public string PreNodeGuid { get; }
        }

        //================================= 노드들 공용 처리 ================================

        /// <summary>Quest 실행 상태를 초기화하고 Quest Start 노드에서 흐름을 시작</summary>
        private static bool StartQuestFlow(IQuestController controller, int questId)
        {
            if (!Registry.GetQuestGraphIndex(questId, out QuestContainer container, out QuestGraphIndex index))
            {
                return false;
            }

            QuestStartNodeData[] starts = index.Nodes.Values.OfType<QuestStartNodeData>().ToArray();
            if (starts.Length != 1)
            {
                Debug.LogError($"[Quest] '{container.name}'에는 Quest Start 노드가 정확히 하나 필요하지만 {starts.Length}개 있습니다.", container);
                return false;
            }
            QuestStartNodeData startData = starts[0];

            // 처음 시작할 때만 진행 기록을 만들고, 기존 기록이 있으면 그대로 사용
            controller.QuestProgress.TryGetValue(container.QuestId, out QuestProgress progress);
            if (progress == null)
            {
                progress = new QuestProgress(container);
                controller.QuestProgress[container.QuestId] = progress;
            }

            if (progress.state != QuestState.NotStarted)
            {
                return false;
            }
            ResetProgress(progress);
            progress.state = QuestState.InProgress;
            int runVersion = progress.runVersion;

            //의존 Quest에게 상태 변경을 알린다
            ResumeDependentQuests(controller, questId);

            /// <summary>
            /// 중첩 실행: 현재 노드 처리가 끝나기 전에 추가 노드 처리를 호출하고, 반환되면 원래 처리로 돌아오는 것.
            /// 거기서 현재 노드가 오염됐을 수 있으니 지속 가능한지 검사하는 과정임
            /// </summary>
            if (!CanContinueQuest(controller, progress, runVersion))
            {
                // 교체된 이전 실행은 중단하되, 같은 실행에서 발생한 오류는 호출부에도 실패로 전달합니다.
                return !IsCurrentRun(controller, progress, runVersion) || progress.state != QuestState.ExecutionError;
            }

            bool executionSucceeded = ExecuteNextNode(controller, container, progress, index, startData.Guid);
            if (IsCurrentRun(controller, progress, runVersion))
            {
                //controller에게 퀘스트의 상태변경을 알림.
                controller.OnQuestProgressChanged(container, progress);
            }

            return executionSucceeded;
        }

        /// <summary>다음 그래프 대기상태 전까지 노드를 쭉 실행</summary>
        private static bool ExecuteNextNode(IQuestController controller, QuestContainer container, QuestProgress progress, QuestGraphIndex index, string sourceGuid)
        {
            int runVersion = progress.runVersion;
            Queue<FlowStep> queue = new ();
            EnqueueNextNodes(index, queue, sourceGuid, null);

            int stepCount = 0;
            while (queue.Count > 0)
            {
                if (!CanContinueQuest(controller, progress, runVersion))
                {
                    return !IsCurrentRun(controller, progress, runVersion) || progress.state != QuestState.ExecutionError;
                }

                FlowStep currentStep = queue.Dequeue();
                NodeBaseData nodeData = currentStep.CurrentNodeData;

                // 이미 완료된 노드는 생략.
                if (progress.CompletedNodeGuids.Contains(nodeData.Guid)
                    || executingActionNodes.Contains((progress, runVersion, nodeData.Guid)))
                {
                    continue;
                }

                if (++stepCount > MaxImmediateNodeSteps)
                {
                    Debug.LogError(
                        $"[Quest] '{container.name}'에서 즉시 실행 단계가 {MaxImmediateNodeSteps}회를 초과했습니다. " +
                        "그래프에 Condition/Action 순환이 있는지 확인하세요.", container);
                    return StopAfterExecutionError(progress);
                }

                //노드 종류별 처리
                switch (nodeData)
                {
                    case QuestStartNodeData:
                    case QuestInteractionEntryNodeData:
                        EnqueueNextNodes(index, queue, nodeData.Guid, null);
                        break;

                    case QuestObjectiveNodeData objectiveData:
                        if (!progress.ActiveNodeGuids.Contains(objectiveData.Guid))
                        {
                            progress.ActiveNodeGuids.Add(objectiveData.Guid);
                        }

                        progress.ObjectiveAmounts.TryAdd(objectiveData.Guid, 0);
                        break;

                    case QuestConditionNodeData conditionData:
                    {
                        QuestExecutionContext context = new (controller, container, progress, conditionData);
                        bool evaluated = QuestMethodInvoker.InvokeMethod(conditionData.Condition, context, MethodKind.Condition, out bool result);
                        if (!CanContinueQuest(controller, progress, runVersion))
                        {
                            return !IsCurrentRun(controller, progress, runVersion) || progress.state != QuestState.ExecutionError;
                        }

                        if (!evaluated)
                        {
                            return StopAfterExecutionError(progress);
                        }

                        EnqueueNextNodes(index, queue, nodeData.Guid, result ? QuestPortNames.True : QuestPortNames.False);
                        break;
                    }

                    case QuestStateConditionNodeData stateConditionData:
                    {
                        controller.QuestProgress.TryGetValue(stateConditionData.QuestId, out QuestProgress inspectedProgress);
                        QuestState currentState = inspectedProgress?.state ?? QuestState.NotStarted;
                        bool result = currentState == stateConditionData.TargetState;
                        EnqueueNextNodes(index, queue, nodeData.Guid, result ? QuestPortNames.True : QuestPortNames.False);
                        break;
                    }

                    case QuestAndGateNodeData ANDGateData:
                        ProcessAndGate(progress, index, queue, ANDGateData, currentStep.PreNodeGuid);
                        break;

                    case QuestFlowEndNodeData flowEndData:
                        if (flowEndData.NewState != QuestState.CanComplete
                            && flowEndData.NewState != QuestState.TurnedIn
                            && flowEndData.NewState != QuestState.Failed)
                        {
                            Debug.LogError(
                                $"[Quest] State Change 노드는 상태를 {flowEndData.NewState}(으)로 변경할 수 없습니다. " +
                                "CanComplete, TurnedIn 또는 Failed를 선택하세요.", container);
                            return StopAfterExecutionError(progress);
                        }

                        progress.state = flowEndData.NewState;
                        progress.ActiveNodeGuids.Clear();
                        CompleteNode(progress, nodeData.Guid);
                        ResumeDependentQuests(controller, progress.questId);
                        return true;

                    case QuestActionNodeData:
                    case QuestRewardNodeData:
                    {
                        MethodBindingData bindingData = nodeData is QuestActionNodeData actionData ? actionData.Action : ((QuestRewardNodeData)nodeData).RewardAction;
                        bool executed = ExecuteAction(controller, container, progress, nodeData, bindingData);
                        if (!CanContinueQuest(controller, progress, runVersion))
                        {
                            return !IsCurrentRun(controller, progress, runVersion) || progress.state != QuestState.ExecutionError;
                        }

                        if (!executed)
                        {
                            return StopAfterExecutionError(progress);
                        }

                        CompleteNode(progress, nodeData.Guid);
                        EnqueueNextNodes(index, queue, nodeData.Guid, null);
                        break;
                    }

                    case QuestStateWaitNodeData stateWaitData:
                    {
                        controller.QuestProgress.TryGetValue(stateWaitData.TargetQuestId, out QuestProgress targetProgress);
                        QuestState targetState = targetProgress?.state ?? QuestState.NotStarted;
                        if (targetState == stateWaitData.RequiredState)
                        {
                            CompleteNode(progress, nodeData.Guid);
                            EnqueueNextNodes(index, queue, nodeData.Guid, null);
                            continue;
                        }

                        if (!progress.ActiveNodeGuids.Contains(nodeData.Guid))
                        {
                            progress.ActiveNodeGuids.Add(nodeData.Guid);
                        }
                        break;
                    }

                    case DialogueCandidateNodeData:
                    case QuestSuggestionNodeData:
                        Debug.LogError($"[Quest] {nodeData.GetType().Name} '{nodeData.Guid}'는 상호작용 경로의 종점입니다.", container);
                        return StopAfterExecutionError(progress);

                    default:
                        Debug.LogError($"[Quest] '{container.name}'에서 지원하지 않는 노드 타입 '{nodeData.GetType().FullName}'을 발견했습니다.", container);
                        return StopAfterExecutionError(progress);
                }
            }

            // 정상적인 진행이 아님을 알리는 방어코드
            if (CanContinueQuest(controller, progress, runVersion)
                && progress.ActiveNodeGuids.Count == 0
                && !executingActionNodes.Any(action => action.Progress == progress && action.RunVersion == runVersion))
            {
                Debug.LogError($"[Quest] '{container.name}'의 진행 경로가 활성 목표나 대기 노드 없이 끝났습니다.", container);
                return StopAfterExecutionError(progress);
            }

            return true;
        }


        /// <summary>조건에 맞는 모든 도착 노드를 실행 대기열에 추가</summary>
        private static void EnqueueNextNodes(QuestGraphIndex index, Queue<FlowStep> queue, string sourceGuid, string sourcePort)
        {
            if (!index.OutputLinksByStartNode.TryGetValue(sourceGuid, out List<NodeLinkData> links))
            {
                return;
            }

            foreach (NodeLinkData link in links)
            {
                if (sourcePort == null || link.StartPortName == sourcePort)
                {
                    queue.Enqueue(new FlowStep(index.Nodes[link.TargetNodeGuid], sourceGuid));
                }
            }
        }


        /// <summary>
        /// progress 의 실행 중 오류 발생
        /// </summary>
        private static bool StopAfterExecutionError(QuestProgress progress)
        {
            progress.state = QuestState.ExecutionError;
            progress.ActiveNodeGuids.Clear();
            return false;
        }

        /// <summary>
        /// 일회성 노드의 실행 완료 처리
        /// </summary>
        private static void CompleteNode(QuestProgress progress, string nodeGuid)
        {
            progress.ActiveNodeGuids.Remove(nodeGuid);

            if (progress.CompletedNodeGuids.Contains(nodeGuid))
            {
                return;
            }

            progress.CompletedNodeGuids.Add(nodeGuid);
        }



        //========================================= 개별 노드 처리 ========================================

        /// <summary>
        /// Quest Action 처리
        /// </summary>
        private static bool ExecuteAction(IQuestController controller, QuestContainer container, QuestProgress progress, NodeBaseData nodeData, MethodBindingData bindingData)
        {
            //Reward는 Action을 지정하지 않아도 통과합니다. 호출 정보 자체가 null이면 오류입니다.
            if (nodeData is QuestRewardNodeData && bindingData != null && !bindingData.HasKey)
            {
                return true;
            }

            if (bindingData == null || !bindingData.HasKey)
            {
                Debug.LogError("[Quest] Action 키가 비어 있습니다.", container);
                return false;
            }

            QuestExecutionContext context = new(controller, container, progress, nodeData);
            var action = (progress, progress.runVersion, nodeData.Guid);
            executingActionNodes.Add(action);
            try
            {
                return QuestMethodInvoker.InvokeMethod(bindingData, context, MethodKind.Action, out _);
            }
            finally
            {
                executingActionNodes.Remove(action);
            }
        }

        /// <summary>
        /// AndGate처리
        /// </summary>
        private static void ProcessAndGate(QuestProgress progress, QuestGraphIndex index, Queue<FlowStep> queue, QuestAndGateNodeData gateData, string sourceNodeGuid)
        {
            string key = $"{gateData.Guid}|{sourceNodeGuid}";
            if (!progress.CompletedANDGateInputs.Contains(key))
            {
                progress.CompletedANDGateInputs.Add(key);
            }

            string prefix = gateData.Guid + "|";

            int arrivedCount = progress.CompletedANDGateInputs.Count(key => key.StartsWith(prefix, StringComparison.Ordinal));
            int requiredCount = index.StartNodeCountByTargetNode[gateData.Guid];
            if (arrivedCount < requiredCount)
            {
                return;
            }

            CompleteNode(progress, gateData.Guid);
            EnqueueNextNodes(index, queue, gateData.Guid, null);
        }
    }
}
