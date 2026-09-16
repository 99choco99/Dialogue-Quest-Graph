using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>게임과 UI에서 Quest 진행을 제어하거나 정보를 조회하는 공개 API입니다.</summary>
    public static partial class QuestManager
    {
        //============================== 등록 ==============================

        /// <summary>게임에서 준비한 Quest 그래프 목록을 등록합니다. 진행 기록은 초기화하지 않습니다.</summary>
        public static void Initialize(IEnumerable<QuestContainer> containers)
        {
            // 생성에 성공한 등록부만 교체하므로 오류가 나면 기존 등록 목록을 유지합니다.
            QuestContainerRegistry.Instance = new QuestContainerRegistry(containers);
        }

        //============================== 조회(Interaction Entry쪽) ==============================

        /// <summary>게임에서 등록한 Quest 그래프를 원래 등록 순서로 반환합니다.</summary>
        public static IReadOnlyList<QuestContainer> RegisteredQuests => Registry.Containers;

        /// <summary>등록한 Quest 그래프 하나를 ID로 찾기</summary>
        public static bool GetQuestContainer(int questId, out QuestContainer container)
        {
            return Registry.GetContainer(questId, out container);
        }

        /// <summary>상호작용 대상 ID와 일치하는 Quest 선택 항목을 원본 순서로 반환합니다.</summary>
        public static QuestSuggestion[] GetQuestSuggestions(IQuestController controller, string interactionTargetId)
        {
            return GetQuestSuggestions(controller, new[] { interactionTargetId });
        }

        /// <summary>여러 상호작용 대상 ID와 일치하는 Quest 선택 항목을 한 번에 조회합니다.</summary>
        public static QuestSuggestion[] GetQuestSuggestions(IQuestController controller, IEnumerable<string> interactionTargetIds)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            return QuestInteractionQuery
                .GetQuestSuggestions(Registry, controller, interactionTargetIds)
                .ToArray();
        }

        /// <summary>등록된 Quest 그래프에서 상호작용 대상 ID와 일치하는 모든 대화 후보를 반환합니다.</summary>
        public static DialogueCandidate[] GetDialogueCandidates(IQuestController controller, string interactionTargetId)
        {
            return GetDialogueCandidates(controller, new[] { interactionTargetId });
        }

        /// <summary>여러 상호작용 대상 ID와 일치하는 대화 후보를 한 번에 조회합니다.</summary>
        public static DialogueCandidate[] GetDialogueCandidates(IQuestController controller, IEnumerable<string> interactionTargetIds)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            return QuestInteractionQuery
                .GetDialogueCandidates(Registry, controller, interactionTargetIds)
                .ToArray();
        }

        //==============================조회(Quest Start )=================================
        //============================= 상태에 따른 퀘스트 목록 가져오기 =======================================

        /// <summary>등록된 Quest 중 아직 시작하지 않은 그래프를 반환합니다. 진행 기록이 없는 Quest도 포함하며, 수락 가능 여부는 검사하지 않습니다.</summary>
        public static QuestContainer[] GetNotStartedQuests(IQuestController controller)
        {
            return GetQuestsByState(controller, QuestState.NotStarted);
        }

        /// <summary>등록된 Quest 중 진행 중인 그래프를 반환합니다.</summary>
        public static QuestContainer[] GetInProgressQuests(IQuestController controller)
        {
            return GetQuestsByState(controller, QuestState.InProgress);
        }

        /// <summary>등록된 Quest 중 완료 보고가 가능한 그래프를 반환합니다.</summary>
        public static QuestContainer[] GetCanCompleteQuests(IQuestController controller)
        {
            return GetQuestsByState(controller, QuestState.CanComplete);
        }

        /// <summary>등록된 Quest 중 완료 보고를 마친 그래프를 반환합니다.</summary>
        public static QuestContainer[] GetTurnedInQuests(IQuestController controller)
        {
            return GetQuestsByState(controller, QuestState.TurnedIn);
        }

        /// <summary>등록된 Quest 중 게임 진행상 실패한 그래프를 반환합니다.</summary>
        public static QuestContainer[] GetFailedQuests(IQuestController controller)
        {
            return GetQuestsByState(controller, QuestState.Failed);
        }

        /// <summary>등록된 Quest 중 실행 오류로 중단된 그래프를 반환합니다.</summary>
        public static QuestContainer[] GetExecutionErrorQuests(IQuestController controller)
        {
            return GetQuestsByState(controller, QuestState.ExecutionError);
        }

        /// <summary>진행 기록을 만들거나 변경하지 않고, 조건에 맞는 그래프를 등록 순서대로 반환합니다.</summary>
        private static QuestContainer[] GetQuestsByState(IQuestController controller, QuestState state)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            List<QuestContainer> containers = new ();
            foreach (QuestContainer container in Registry.Containers)
            {
                controller.QuestProgress.TryGetValue(container.QuestId, out QuestProgress progress);
                QuestState currentState = progress?.state ?? QuestState.NotStarted;
                if (currentState == state)
                {
                    containers.Add(container);
                }
            }

            return containers.ToArray();
        }

        /// <summary>현재 활성화된 목표를 반환합니다.</summary>
        public static QuestObjectiveInfo[] GetCurrentObjectives(IQuestController controller, int questId)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            controller.QuestProgress.TryGetValue(questId, out QuestProgress progress);
            if (!Registry.GetQuestGraphIndex(questId, out _, out QuestGraphIndex index) || progress == null)
            {
                return Array.Empty<QuestObjectiveInfo>();
            }

            List<QuestObjectiveInfo> objectiveInfos = new ();
            foreach (string guid in progress.ActiveNodeGuids)
            {
                if (!index.Nodes.TryGetValue(guid, out NodeBaseData nodeData))
                {
                    continue;
                }

                if (nodeData is QuestObjectiveNodeData objectiveData)
                {
                    progress.ObjectiveAmounts.TryGetValue(guid, out int count);
                    objectiveInfos.Add(new QuestObjectiveInfo(questId, objectiveData, count));
                }
            }

            return objectiveInfos.ToArray();
        }

        //=========================== 시작 및 수락 ===========================

        /// <summary>Quest를 수락하는 함수. 시작 전 유효한지 확인합니다.</summary>
        public static bool StartQuest(IQuestController controller, QuestSuggestion suggestion)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            if (suggestion == null)
            {
                throw new ArgumentNullException(nameof(suggestion), "수락할 Quest 선택 항목가 필요합니다.");
            }

            if (!QuestInteractionQuery.RefreshQuestSuggestion(Registry, controller, suggestion, out QuestSuggestion refreshedSuggestion)
                || !refreshedSuggestion.IsAvailable)
            {
                return false;
            }

            return StartQuestFlow(controller, refreshedSuggestion.QuestId);
        }

        /// <summary>
        /// Quest를 명시적으로 시작합니다. <para></para>
        /// 게임 흐름이 시작을 이미 결정한 경우에 사용합니다.
        /// </summary>
        /// <remarks>NotStarted 상태에서만 시작합니다. 재시작하려면 먼저 ResetQuest를 호출하세요.</remarks>
        public static bool StartQuest(IQuestController controller, int questId)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            return StartQuestFlow(controller, questId);
        }

        //=========================== 목표 진행 =============================

        /// <summary>이벤트와 일치하는 목표들의 진행량을 반영하고, 달성하면 다음 노드 실행</summary>
        public static void ProcessObjectivesByEvent(IQuestController controller, string eventKey, int objectiveTargetId, int amount)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            QuestContainerRegistry registry = Registry;
            if (string.IsNullOrWhiteSpace(eventKey) || amount <= 0)
            {
                return;
            }

            eventKey = eventKey.Trim();

            // 이번 이벤트가 시작될 때의 runVersion과 activeNode를 보관. runVersion과 activeNode를 참조값으로 넘기지 않음.
            var targets = controller.QuestProgress.Values
                .Where(progress => progress != null && progress.state == QuestState.InProgress)
                .Select(progress => (Progress: progress, RunVersion: progress.runVersion, ActiveNodeGuids: progress.ActiveNodeGuids.ToArray()))
                .ToArray();

            foreach (var target in targets)
            {
                QuestProgress progress = target.Progress;
                if (!registry.GetQuestGraphIndex(progress.questId, out QuestContainer container, out QuestGraphIndex index))
                {
                    continue;
                }

                int runVersion = target.RunVersion;
                bool changed = false;
                foreach (string activeGuid in target.ActiveNodeGuids)
                {
                    if (!CanContinueQuest(controller, progress, runVersion))
                    {
                        break;
                    }

                    if (!progress.ActiveNodeGuids.Contains(activeGuid))
                    {
                        continue;
                    }

                    if (!index.Nodes.TryGetValue(activeGuid, out NodeBaseData activeNodeData)
                        || activeNodeData is not QuestObjectiveNodeData objectiveData
                        || objectiveData.EventKey != eventKey
                        || objectiveData.TargetId != objectiveTargetId)
                    {
                        continue;
                    }

                    changed |= ProcessObjectiveProgress(controller, container, progress, index, objectiveData, amount, out bool executionSucceeded);

                    if (!executionSucceeded || progress.state != QuestState.InProgress)
                    {
                        break;
                    }
                }

                if (changed && IsCurrentRun(controller, progress, runVersion))
                {
                    controller.OnQuestProgressChanged(container, progress);
                }
            }
        }

        /// <summary>GUID로 지정한 목표 하나의 진행량을 반영하고, 달성하면 다음 노드 실행</summary>
        public static bool ProcessObjectiveByGuid(IQuestController controller, int questId, string objectiveNodeGuid, int amount = 1)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            if (string.IsNullOrWhiteSpace(objectiveNodeGuid) || amount <= 0)
            {
                return false;
            }

            controller.QuestProgress.TryGetValue(questId, out QuestProgress progress);
            if (!Registry.GetQuestGraphIndex(questId, out QuestContainer container, out QuestGraphIndex index)
                || progress == null || progress.state != QuestState.InProgress)
            {
                return false;
            }

            if (!progress.ActiveNodeGuids.Contains(objectiveNodeGuid)
                || !index.Nodes.TryGetValue(objectiveNodeGuid, out NodeBaseData nodeData)
                || nodeData is not QuestObjectiveNodeData objectiveData)
            {
                return false;
            }

            int runVersion = progress.runVersion;
            bool changed = ProcessObjectiveProgress(controller, container, progress, index, objectiveData, amount, out bool executionSucceeded);
            if (changed && IsCurrentRun(controller, progress, runVersion))
            {
                controller.OnQuestProgressChanged(container, progress);
            }

            return changed && executionSucceeded;
        }

        //========================= 상태 변경 및 복원 =========================

        /// <summary>Quest 상태와 모든 노드 진행 기록을 시작 전 상태로 초기화</summary>
        public static bool ResetQuest(IQuestController controller, int questId)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            controller.QuestProgress.TryGetValue(questId, out QuestProgress progress);
            if (!Registry.GetContainer(questId, out QuestContainer container) || progress == null)
            {
                return false;
            }

            ResetProgress(progress);
            progress.state = QuestState.NotStarted;

            // 다시 시작하기 전에 NotStarted 대기를 먼저 처리
            int runVersion = progress.runVersion;
            ResumeDependentQuests(controller, questId);

            if (IsCurrentRun(controller, progress, runVersion))
            {
                controller.OnQuestProgressChanged(container, progress);
            }
            return true;
        }

        /// <summary>누적 진행 기록은 유지하고 상태를 변경하며, 진행이 끝나는 상태에서는 활성 노드를 정리합니다.</summary>
        public static bool SetQuestState(IQuestController controller, int questId, QuestState state)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            if (!Enum.IsDefined(typeof(QuestState), state))
            {
                return false;
            }

            if (state == QuestState.NotStarted)
            {
                return ResetQuest(controller, questId);
            }

            controller.QuestProgress.TryGetValue(questId, out QuestProgress progress);
            if (!Registry.GetContainer(questId, out QuestContainer container) || progress == null)
            {
                return false;
            }

            if (state == QuestState.InProgress && progress.ActiveNodeGuids.Count == 0)
            {
                return false;
            }

            progress.state = state;
            if (state != QuestState.InProgress)
            {
                progress.ActiveNodeGuids.Clear();
            }

            //상태를 다시 바꾸기 전에 의존 Quest의 대기를 처리
            int runVersion = progress.runVersion;
            ResumeDependentQuests(controller, questId);

            if (IsCurrentRun(controller, progress, runVersion))
            {
                controller.OnQuestProgressChanged(container, progress);
            }
            return true;
        }

        //============================== 저장 및 복원 ==============================

        /// <summary>현재 퀘스트 진행 기록을 저장 데이터로 가져옵니다. 파일 저장은 게임의 저장 시스템에서 따로 처리해야 합니다.</summary>
        public static QuestSaveData CaptureSaveData(IQuestController controller)
        {
            return QuestSaveData.Capture(controller);
        }

        /// <summary>저장 데이터를 검증한 뒤 진행 기록을 교체하거나 병합합니다. 노드 복원과 변경 알림(ResumeRestoredQuests)은 별도</summary>
        public static bool RestoreSaveData(IQuestController controller, QuestSaveData saveData, bool shouldClear, out string error)
        {
            if (saveData == null)
            {
                error = "복원할 Quest 저장 데이터가 없습니다.";
                return false;
            }

            return saveData.Restore(controller, out error, shouldClear);
        }

        /// <summary>복원된 퀘스트들을 다시 재개</summary>
        public static void ResumeRestoredQuests(IQuestController controller)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller), "IQuestController를 구현한 객체를 controller에 전달하세요.");
            }

            QuestContainerRegistry registry = Registry;

            foreach (int questId in controller.QuestProgress.Keys.ToArray())
            {
                //현재 상태의 데이터로 읽기
                if (!controller.QuestProgress.TryGetValue(questId, out QuestProgress progress)
                    || progress == null
                    || (progress.state != QuestState.InProgress && progress.state != QuestState.CanComplete))
                {
                    continue;
                }

                registry.GetContainer(progress.questId, out QuestContainer container);
                if (container == null)
                {
                    Debug.LogWarning($"[Quest] 저장 데이터가 알 수 없는 Quest ID {progress.questId}를 참조합니다.");
                    continue;
                }

                controller.OnQuestProgressChanged(container, progress);
            }

            // 복원된 상태를 먼저 표시한 뒤 정상 진행을 재개. 다른 게임 데이터도 복원된 뒤여야 함.
            foreach (int questId in registry.Containers.Select(container => container.QuestId).ToArray())
            {
                ResumeDependentQuests(controller, questId);
            }
        }
    }
}
