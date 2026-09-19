using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UniversalGraph
{
    /// <summary>
    /// Quest 진행 그래프를 실행하는 런타임 해석기
    /// <para>설계도를 읽어서 실제 진행 기록을 바꾸는 실행기</para>
    /// </summary>
    public static partial class QuestManager
    {
        //==================================진행도 초기화================================

        /// <summary>
        /// 퀘스트 진행도 초기화
        /// </summary>
        private static void ResetProgress(QuestProgress progress)
        {
            progress.runVersion++;
            progress.ActiveNodeGuids.Clear();
            progress.ObjectiveAmounts.Clear();
            progress.CompletedNodeGuids.Clear();
            progress.CompletedANDGateInputs.Clear();
        }

        //================================= 유효성 검사 =================================

        /// <summary>지금도 같은 진행 기록, 같은 실행 회차인가?</summary>
        private static bool IsCurrentRun(IQuestController controller, QuestProgress originalProgress, int runVersion)
        {
            return originalProgress.runVersion == runVersion
                   && controller.QuestProgress.TryGetValue(originalProgress.questId, out QuestProgress currentProgress)
                   && ReferenceEquals(currentProgress, originalProgress);
        }

        /// <summary>게임 Action이 퀘스트를 종료, 초기화했다면 이전 흐름을 중단</summary>
        private static bool CanContinueQuest(IQuestController controller, QuestProgress progress, int runVersion)
        {
            return (progress.state == QuestState.InProgress
                    || progress.state == QuestState.CanComplete)
                   && IsCurrentRun(controller, progress, runVersion);
        }

        
        //=============================== 진행 ===================================

        /// <summary>목표 진행량을 반영하고 완료되면 연결된 Quest 흐름을 계속 실행</summary>
        private static bool ProcessObjectiveProgress(
            IQuestController controller,QuestContainer container, QuestProgress progress,
            QuestGraphIndex index, QuestObjectiveNodeData objectiveData, int amount, out bool executionSucceeded)
        {
            executionSucceeded = true;

            int requiredAmount = objectiveData.RequiredAmount;
            progress.ObjectiveAmounts.TryGetValue(objectiveData.Guid, out int currentAmount);
            int nextAmount = (int)Math.Min(requiredAmount, (long)currentAmount + amount); //오버플로우 방지
            if (nextAmount == currentAmount)
            {
                return false;
            }

            progress.ObjectiveAmounts[objectiveData.Guid] = nextAmount;
            if (nextAmount < requiredAmount)
            {
                return true;
            }

            CompleteNode(progress, objectiveData.Guid);
            executionSucceeded = ExecuteNextNode(controller, container, progress, index, objectiveData.Guid);
            return true;
        }

        /// <summary>해당 퀘스트의 변화를 기다리고 있던 퀘스트들에게 변화됐음을 알리는 함수</summary>
        private static void ResumeDependentQuests(IQuestController controller, int questId)
        {
            QuestContainerRegistry registry = QuestContainerRegistry.Instance;

            controller.QuestProgress.TryGetValue(questId, out QuestProgress changedProgress);
            QuestState changedState = changedProgress?.state ?? QuestState.NotStarted;
            // 실행 오류일 때
            if (changedState == QuestState.ExecutionError)
            {
                return;
            }

            // 후속 Action이 상태를 또 바꾸기 전에, 이번 도달 순간에 조건을 만족한 대기만 확정
            var waitingQuests = new List<(QuestProgress Progress, int RunVersion, QuestContainer Container, QuestGraphIndex Index, string[] NodeGuids)>();
            foreach (QuestProgress progress in controller.QuestProgress.Values
                         .Where(progress => progress != null && (progress.state == QuestState.InProgress || progress.state == QuestState.CanComplete))
                         .ToArray())
            {
                if (!registry.GetQuestGraphIndex(progress.questId, out QuestContainer container, out QuestGraphIndex index))
                {
                    continue;
                }

                string[] nodeGuids = progress.ActiveNodeGuids
                    .Where(guid => index.Nodes.TryGetValue(guid, out NodeBaseData data)
                        && data is QuestStateWaitNodeData waitData
                        && waitData.TargetQuestId == questId && waitData.RequiredState == changedState)
                    .ToArray();
                if (nodeGuids.Length > 0)
                {
                    waitingQuests.Add((progress, progress.runVersion, container, index, nodeGuids));
                }
            }

            foreach (var waiting in waitingQuests)
            {
                QuestProgress progress = waiting.Progress;
                int runVersion = waiting.RunVersion;
                bool resumed = false;
                foreach (string activeGuid in waiting.NodeGuids)
                {
                    if (!CanContinueQuest(controller, progress, runVersion))
                    {
                        break;
                    }

                    // 다른 후속 흐름에서 이미 처리한 대기는 중복 실행하지 않습니다.
                    if (!progress.ActiveNodeGuids.Contains(activeGuid))
                    {
                        continue;
                    }

                    CompleteNode(progress, activeGuid);
                    resumed = true;

                    if (!ExecuteNextNode(controller, waiting.Container, progress, waiting.Index, activeGuid))
                    {
                        break;
                    }
                }

                if (resumed && IsCurrentRun(controller, progress, runVersion))
                {
                    controller.OnQuestProgressChanged(waiting.Container, progress);
                }
            }
        }
    }
}
