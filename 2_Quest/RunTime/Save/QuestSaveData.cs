using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalGraph
{
    /// <summary>퀘스트 진행 기록을 담는 저장용 데이터. 저장 형식과 파일 입출력은 게임에서 처리</summary>
    [Serializable]
    public sealed class QuestSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public List<QuestProgress> QuestList = new();

        /// <summary>모든 진행 기록을 검증 후 Quest ID 순서로 저장용 데이터에 복사</summary>
        internal static QuestSaveData Capture(IQuestController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            if (controller.QuestProgress == null)
            {
                throw new InvalidOperationException("IQuestController.QuestProgress가 null을 반환했습니다.");
            }

            QuestSaveData saveData = new ();
            QuestContainerRegistry registry = QuestContainerRegistry.Instance;
            var progresses = controller.QuestProgress.Values
                            .Where(progress => progress != null)
                            .OrderBy(progress => progress.questId);

            foreach (QuestProgress progress in progresses)
            {
                if (!ValidateProgress(progress, registry, out string error))
                {
                    throw new InvalidOperationException(error);
                }

                saveData.QuestList.Add(CopyProgress(progress));
            }
            return saveData;
        }

        /// <summary>저장된 진행 기록을 검증하고 복사하여 Controller에 반영</summary>
        internal bool Restore(IQuestController controller, out string error, bool shouldClear = true)
        {
            if (controller == null)
            {
                error = "IQuestController를 구현한 객체를 controller에 전달하세요.";
                return false;
            }

            if (controller.QuestProgress == null)
            {
                error = "IQuestController.QuestProgress가 null을 반환했습니다.";
                return false;
            }

            if (schemaVersion != CurrentSchemaVersion)
            {
                error = $"Quest 저장 데이터의 스키마 버전 {schemaVersion}은 지원하지 않습니다.";
                return false;
            }

            if (QuestList == null)
            {
                error = "Quest 저장 데이터에 QuestList 목록이 없습니다. 빈 저장은 빈 목록을 사용하세요.";
                return false;
            }

            QuestContainerRegistry registry = QuestContainerRegistry.Instance;

            Dictionary<int, QuestProgress> restored = new ();
            foreach (QuestProgress progress in QuestList)
            {
                if (!ValidateProgress(progress, registry, out error))
                {
                    return false;
                }

                if (!restored.TryAdd(progress.questId, CopyProgress(progress)))
                {
                    error = $"Quest 저장 데이터에 중복된 Quest ID {progress.questId}가 있습니다.";
                    return false;
                }
            }

            if (shouldClear)
            {
                controller.QuestProgress.Clear();
            }

            //대입
            foreach (KeyValuePair<int, QuestProgress> pair in restored)
            {
                controller.QuestProgress[pair.Key] = pair.Value;
            }

            error = null;
            return true;
        }

        /// <summary>progress를 깊은 복사 후 복사본을 반환</summary>
        private static QuestProgress CopyProgress(QuestProgress progress)
        {
            QuestProgress copy = new ()
            {
                questId = progress.questId,
                graphSchemaVersion = progress.graphSchemaVersion,
                state = progress.state
            };

            copy.ActiveNodeGuids.AddRange(progress.ActiveNodeGuids);
            copy.CompletedNodeGuids.AddRange(progress.CompletedNodeGuids);
            copy.CompletedANDGateInputs.AddRange(progress.CompletedANDGateInputs);

            foreach (KeyValuePair<string, int> pair in progress.ObjectiveAmounts)
            {
                copy.ObjectiveAmounts.Add(pair.Key, pair.Value);
            }

            return copy;
        }

        //===================================== Validation 함수들 ==============================

        /// <summary>Quest 등록 여부, 스키마 버전, 상태와 진행 기록의 일치 여부 검사</summary>
        private static bool ValidateProgress(QuestProgress progress, QuestContainerRegistry registry, out string error)
        {
            if (progress == null)
            {
                error = "Quest 저장 데이터에 null 진행 기록이 있습니다.";
                return false;
            }

            if (!Enum.IsDefined(typeof(QuestState), progress.state))
            {
                error = $"Quest {progress.questId}에 알 수 없는 상태 값 {(int)progress.state}이 있습니다.";
                return false;
            }

            //중복검사
            if (!ValidateGuidList(progress.ActiveNodeGuids, "활성 노드", out error)
                || !ValidateGuidList(progress.CompletedNodeGuids, "완료 노드", out error)
                || !ValidateGuidList(progress.CompletedANDGateInputs, "완료 Gate 입력", out error))
            {
                error = $"Quest {progress.questId}: {error}";
                return false;
            }

            if (!registry.GetQuestGraphIndex(progress.questId, out QuestContainer container, out QuestGraphIndex graphIndex))
            {
                error = $"Quest 저장 데이터가 등록되지 않았거나 읽을 수 없는 Quest ID {progress.questId}를 참조합니다.";
                return false;
            }

            if (progress.graphSchemaVersion != container.SchemaVersion)
            {
                error = $"Quest {progress.questId} 저장 데이터의 정의 스키마는 " +
                        $"{progress.graphSchemaVersion}이지만 등록된 정의는 {container.SchemaVersion}입니다.";
                return false;
            }

            //노드 정보 검사
            if (progress.state == QuestState.InProgress && progress.ActiveNodeGuids.Count == 0)
            {
                error = $"Quest {progress.questId}가 InProgress이지만 활성 목표나 대기 노드가 없습니다.";
                return false;
            }

            if (progress.state != QuestState.CanComplete && progress.state != QuestState.InProgress && progress.ActiveNodeGuids.Count > 0)
            {
                error = $"Quest {progress.questId}의 상태는 {progress.state}이지만 활성 노드가 남아 있습니다.";
                return false;
            }

            if (progress.state == QuestState.NotStarted
                && (progress.ObjectiveAmounts.Count > 0 || progress.CompletedNodeGuids.Count > 0 || progress.CompletedANDGateInputs.Count > 0))
            {
                error = $"Quest {progress.questId}가 NotStarted이지만 이전 진행 기록이 남아 있습니다.";
                return false;
            }

            return ValidateNodeRecords(progress, graphIndex, out error);
        }

        /// <summary>
        /// node Guid들이 유효한건지 검사
        /// </summary>
        private static bool ValidateGuidList(IEnumerable<string> values, string label, out string error)
        {
            HashSet<string> unique = new ();
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    error = $"빈 {label} 키가 있습니다.";
                    return false;
                }

                if (!unique.Add(value))
                {
                    error = $"중복된 {label} 키 '{value}'가 있습니다.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>등록된 노드의 존재, 타입, 목표 진행량, AND Gate 연결 검사</summary>
        private static bool ValidateNodeRecords(QuestProgress progress, QuestGraphIndex graphIndex, out string error)
        {
            foreach (string activeGuid in progress.ActiveNodeGuids)
            {
                if (!graphIndex.Nodes.TryGetValue(activeGuid, out NodeBaseData nodeData))
                {
                    error = $"Quest {progress.questId}의 활성 노드 '{activeGuid}'가 현재 정의에 없습니다.";
                    return false;
                }

                if (nodeData is not QuestObjectiveNodeData && nodeData is not QuestStateWaitNodeData)
                {
                    error = $"Quest {progress.questId}의 활성 노드 '{activeGuid}' 타입 " +
                            $"'{nodeData.GetType().Name}'은 대기 가능한 노드가 아닙니다.";
                    return false;
                }

                if (progress.CompletedNodeGuids.Contains(activeGuid))
                {
                    error = $"Quest {progress.questId}의 노드 '{activeGuid}'가 활성 목록과 완료 목록에 모두 있습니다.";
                    return false;
                }
            }

            foreach (string completedGuid in progress.CompletedNodeGuids)
            {
                if (!graphIndex.Nodes.TryGetValue(completedGuid, out NodeBaseData completedNodeData))
                {
                    error = $"Quest {progress.questId}의 완료 노드 '{completedGuid}'가 현재 정의에 없습니다.";
                    return false;
                }

                if (!CanStoreCompletedNode(completedNodeData))
                {
                    error = $"Quest {progress.questId}의 완료 노드 '{completedGuid}' 타입 " +
                            $"'{completedNodeData.GetType().Name}'은 완료 기록을 남기는 노드가 아닙니다.";
                    return false;
                }

                if (completedNodeData is QuestObjectiveNodeData objectiveData
                    && (!progress.ObjectiveAmounts.TryGetValue(completedGuid, out int currentAmount)
                        || currentAmount != objectiveData.RequiredAmount))
                {
                    error = $"Quest {progress.questId}의 완료된 Objective '{completedGuid}' 진행량 기록이 필요량 {objectiveData.RequiredAmount}과 일치하지 않습니다.";
                    return false;
                }
            }

            foreach (KeyValuePair<string, int> pair in progress.ObjectiveAmounts)
            {
                if (!graphIndex.Nodes.TryGetValue(pair.Key, out NodeBaseData nodeData)
                    || nodeData is not QuestObjectiveNodeData objectiveData)
                {
                    error = $"Quest {progress.questId}의 진행량 키 '{pair.Key}'가 현재 Objective 노드를 참조하지 않습니다.";
                    return false;
                }

                int requiredAmount = objectiveData.RequiredAmount;
                if (pair.Value < 0 || pair.Value > requiredAmount)
                {
                    error = $"Quest {progress.questId}의 Objective '{pair.Key}' 진행량 {pair.Value}가 " +
                            $"0~{requiredAmount} 범위를 벗어났습니다.";
                    return false;
                }

                bool isActive = progress.ActiveNodeGuids.Contains(pair.Key);
                if (isActive && pair.Value >= requiredAmount)
                {
                    error = $"Quest {progress.questId}의 활성 Objective '{pair.Key}' 진행량이 " +
                            $"이미 필요량 {requiredAmount}에 도달했습니다.";
                    return false;
                }

                // 종료된 Quest는 활성 목표를 비우지만, 미완료 목표의 진행량은 기록으로 보존합니다.
                if ((progress.state == QuestState.CanComplete || progress.state == QuestState.InProgress) && !isActive && !progress.CompletedNodeGuids.Contains(pair.Key))
                {
                    error = $"Quest {progress.questId}의 Objective '{pair.Key}' 진행량이 " +
                            "활성 또는 완료 기록과 연결되어 있지 않습니다.";
                    return false;
                }
            }

            foreach (string gateInput in progress.CompletedANDGateInputs)
            {
                int separatorIndex = gateInput.IndexOf('|');
                if (separatorIndex <= 0 || separatorIndex >= gateInput.Length - 1)
                {
                    error = $"Quest {progress.questId}의 완료 Gate 입력 '{gateInput}' 형식이 올바르지 않습니다.";
                    return false;
                }

                string gateGuid = gateInput.Substring(0, separatorIndex);
                string sourceGuid = gateInput.Substring(separatorIndex + 1);
                if (!graphIndex.Nodes.TryGetValue(gateGuid, out NodeBaseData gateData)
                    || gateData is not QuestAndGateNodeData
                    || !graphIndex.OutputLinksByStartNode.TryGetValue(sourceGuid, out List<NodeLinkData> sourceLinks)
                    || !sourceLinks.Any(link => link.TargetNodeGuid == gateGuid))
                {
                    error = $"Quest {progress.questId}의 완료 Gate 입력 '{gateInput}'이 현재 연결 구조와 일치하지 않습니다.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 완료 기록을 남길 수 있는 노드 타입인지 검사
        /// </summary>
        private static bool CanStoreCompletedNode(NodeBaseData nodeData)
        {
            return nodeData is QuestObjectiveNodeData
                   || nodeData is QuestAndGateNodeData
                   || nodeData is QuestStateChangeNodeData
                   || nodeData is QuestActionNodeData
                   || nodeData is QuestRewardNodeData
                   || nodeData is QuestStateWaitNodeData;
        }
    }
}
