using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>Dialogue 노드 종류별 실행과 선택지 평가를 담당합니다.</summary>
    public sealed partial class DialogueManager
    {
        /// <summary>현재 노드를 실행, 처리</summary>
        private void RunUntilBlocked()
        {
            if (isNodeProcessing)
            {
                return;
            }

            isNodeProcessing = true;
            int stepCount = 0;
            try
            {
                while (IsConversationActive && blockKind == BlockKind.None)
                {
                    if (currentNodeData == null)
                    {
                        FailConversation("[Dialogue] 현재 노드가 예기치 않게 null이 되었습니다.");
                        break;
                    }

                    //순한 참조를 막기 위해 노드의 수를 체크
                    if (++stepCount > MaxNodeStepsPerProcess)
                    {
                        FailConversation(
                            $"[Dialogue] 한 번의 동기 진행에서 처리한 노드가 최대 허용 횟수 {MaxNodeStepsPerProcess}회를 초과했습니다. " +
                            "즉시 진행되는 노드 또는 UI 자동 진행의 순환 연결이 있는지 확인하세요.");
                        break;
                    }

                    //현재 노드 종류에 따라 다르게 처리
                    switch (currentNodeData)
                    {
                        case DialogueConditionNodeData conditionData:
                            ProcessCondition(conditionData);
                            break;

                        case DialogueActionNodeData actionData:
                            ProcessAction(actionData);
                            break;

                        case DialogueEndNodeData:
                            EndConversation(DialogueEndReason.Completed);
                            break;

                        case DialogueWaitNodeData waitData:
                            ProcessWait(waitData);
                            break;

                        case DialogueWaitSignalNodeData waitSignalData:
                            ProcessSignalWait(waitSignalData);
                            break;

                        case DialogueLineNodeData lineData:
                            ProcessDialogueLine(lineData);
                            break;

                        case DialogueChoiceNodeData choiceData:
                            ProcessChoiceNode(choiceData);
                            break;

                        default:
                            FailConversation($"[Dialogue] 지원하지 않는 노드 타입입니다: '{currentNodeData.GetType().FullName}'.");
                            break;
                    }
                }
            }
            finally
            {
                isNodeProcessing = false;
            }

            InvokePendingCompletionCallbacks();
        }


        //======================================각 노드들의 처리 방법에 대한 함수들 ===============================

        /// <summary>
        /// Condition 노드를 처리
        /// </summary>
        private void ProcessCondition(DialogueConditionNodeData data)
        {
            int conversationId = activeConversationId;
            bool evaluated = DialogueMethodInvoker.InvokeMethod(data.Condition, currentContext, MethodKind.Condition, out bool result);

            if (!IsCurrentConversation(conversationId, data))
            {
                return;
            }

            if (!evaluated)
            {
                EndConversation(DialogueEndReason.Faulted);
                return;
            }

            MoveToNextNode(data.Guid, result ? DialoguePortNames.True : DialoguePortNames.False);
        }


        /// <summary>
        /// Action 노드를 처리
        /// </summary>
        private void ProcessAction(DialogueActionNodeData data)
        {
            int conversationId = activeConversationId;
            bool executed = DialogueMethodInvoker.InvokeMethod(data.Action, currentContext, MethodKind.Action, out _);

            if (!IsCurrentConversation(conversationId, data))
            {
                return;
            }

            if (!executed)
            {
                EndConversation(DialogueEndReason.Faulted);
                return;
            }

            MoveToNextNode(data.Guid, DialoguePortNames.Next);
        }


        /// <summary>
        /// Wait 노드를 처리 , Tick으로 시간처리함
        /// </summary>
        private void ProcessWait(DialogueWaitNodeData data)
        {
            float duration = data.DurationSeconds;
            if (duration < 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                FailConversation($"[Dialogue] Wait 노드 '{data.Guid}'의 대기 시간 '{duration}'이(가) 올바르지 않습니다.");
                return;
            }

            if (duration == 0f)
            {
                MoveToNextNode(data.Guid, DialoguePortNames.Next);
                return;
            }

            blockKind = BlockKind.Time;
            waitTimeLeft = duration;
            useUnscaledTime = data.UseUnscaledTime;
        }


        /// <summary>
        /// SignalWait 노드 처리
        /// </summary>
        private void ProcessSignalWait(DialogueWaitSignalNodeData data)
        {
            string signalKey = data.SignalKey;
            if (string.IsNullOrWhiteSpace(signalKey))
            {
                FailConversation($"[Dialogue] Wait Signal 노드 '{data.Guid}'에 Signal 키가 없습니다.");
                return;
            }

            blockKind = BlockKind.Signal;
            waitSignalKey = signalKey;
        }

        /// <summary>
        /// 기본 대화문 노드 처리
        /// </summary>
        private void ProcessDialogueLine(DialogueLineNodeData data)
        {
            blockKind = BlockKind.Line;
            currentPromptId = ++promptCounter;

            InvokeDuringConversation(ShowLine, data, nameof(ShowLine), activeConversationId, data);
        }


        /// <summary>표시 가능한 선택지를 준비하고 선택 입력을 대기</summary>
        private void ProcessChoiceNode(DialogueChoiceNodeData data)
        {
            int conversationId = activeConversationId;

            if (!CollectVisibleChoices(data, conversationId))
            {
                return;
            }

            if (visibleChoices.Count == 0)
            {
                MoveToNextNode(data.Guid, DialoguePortNames.Default);
                return;
            }

            blockKind = BlockKind.Choice;
            currentPromptId = ++promptCounter;

            IReadOnlyList<DialogueChoiceData> choicesToShow = visibleChoices.ToArray();
            InvokeDuringConversation(ShowChoices, choicesToShow, nameof(ShowChoices), conversationId, data);
        }

        /// <summary>선택지별 조건을 평가하고 현재 진입에서 사용할 수 있는 선택지만 보관</summary>
        private bool CollectVisibleChoices(DialogueChoiceNodeData data, int conversationId)
        {
            visibleChoices.Clear();
            foreach (DialogueChoiceData choiceData in data.Choices)
            {
                //조건이 없으면 일단 띄움
                if (!choiceData.VisibilityCondition.HasKey)
                {
                    visibleChoices.Add(choiceData);
                    continue;
                }

                bool evaluated = DialogueMethodInvoker.InvokeMethod(choiceData.VisibilityCondition, currentContext, MethodKind.Condition, out bool visible);
                if (!IsCurrentConversation(conversationId, data))
                {
                    return false;
                }

                if (!evaluated)
                {
                    EndConversation(DialogueEndReason.Faulted);
                    return false;
                }

                if (visible)
                {
                    visibleChoices.Add(choiceData);
                }
            }

            return true;
        }



        //===========================진행 중 대화 알림================================

        /// <summary>진행 중인 대화의 알림을 전달하고, 대화가 바뀌거나 예외가 발생하면 호출을 중단</summary>
        private void InvokeDuringConversation(Action callbacks, string notificationName, int conversationId)
        {
            if (callbacks == null)
            {
                return;
            }

            foreach (Action callback in callbacks.GetInvocationList())
            {
                try
                {
                    callback.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Dialogue] {notificationName} 콜백 실행 중 예외가 발생했습니다.\n{exception}");
                    if (IsCurrentConversation(conversationId, null))
                    {
                        EndConversation(DialogueEndReason.Faulted);
                    }
                    return;
                }

                if (!IsCurrentConversation(conversationId))
                {
                    return;
                }
            }
        }


        /// <summary>데이터를 포함한 진행 중 대화 알림을 전달하고, 대화가 바뀌거나 예외가 발생하면 호출을 중단</summary>
        private void InvokeDuringConversation<T>(Action<T> callbacks, T value, string notificationName, int conversationId, NodeBaseData nodeData = null)
        {
            if (callbacks == null)
            {
                return;
            }

            foreach (Action<T> callback in callbacks.GetInvocationList())
            {
                try
                {
                    callback.Invoke(value);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Dialogue] {notificationName} 콜백 실행 중 예외가 발생했습니다.\n{exception}");
                    if (IsCurrentConversation(conversationId, nodeData))
                    {
                        EndConversation(DialogueEndReason.Faulted);
                    }
                    return;
                }

                if (!IsCurrentConversation(conversationId, nodeData))
                {
                    return;
                }
            }
        }

    }
}
