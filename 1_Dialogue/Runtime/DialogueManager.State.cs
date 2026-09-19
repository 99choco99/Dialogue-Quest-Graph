using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>DialogueManager의 인스턴스별 런타임 상태와 기본 인스턴스 초기화를 담당</summary>
    public sealed partial class DialogueManager
    {
        private enum BlockKind
        {
            None,
            Line,
            Choice,
            Time,
            Signal
        }


        /// <summary>즉시 진행되는 노드의 무한 순환을 막는 최대 처리 횟수</summary>
        private const int MaxNodeStepsPerProcess = 256;


        //===========================싱글턴 초기화================================

        private static DialogueManager instance;

        /// <summary>다른 대화 실행기와 상태를 공유하지 않는 실행기를 만듭니다.</summary>
        public DialogueManager() { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance?.ResetManagerState();
            instance = null;
        }


        //===========================그래프 조회 인덱스================================

        private readonly Dictionary<(string NodeGuid, string PortName), NodeLinkData> linkDataByOutput = new();
        private readonly Dictionary<string, NodeBaseData> nodeDataByGuid = new();
        private readonly Dictionary<string, DialogueEntryNodeData> entryDataById = new();

        /// <summary>
        /// 그래프 데이터를 빠르게 조회할 수 있도록 인덱스를 생성
        /// </summary>
        private bool BuildGraphIndex(DialogueContainer container, out string error)
        {
            nodeDataByGuid.Clear();
            linkDataByOutput.Clear();
            entryDataById.Clear();

            if (container.Nodes == null || container.Nodes.Count == 0)
            {
                error = $"대화 그래프 '{container.name}'에 노드가 없습니다.";
                return false;
            }

            if (container.NodeLinks == null)
            {
                error = $"대화 그래프 '{container.name}'의 연결선 목록이 null입니다.";
                return false;
            }

            //노드 정보로 딕셔너리 채우기
            foreach (NodeBaseData nodeData in container.Nodes)
            {
                if (nodeData == null || string.IsNullOrWhiteSpace(nodeData.Guid) || !nodeDataByGuid.TryAdd(nodeData.Guid, nodeData))
                {
                    error = $"대화 그래프 '{container.name}'에 null 데이터, 빈 GUID 또는 중복 GUID가 있습니다.";
                    return false;
                }

                switch (nodeData)
                {
                    case DialogueActionNodeData actionData when actionData.Action == null:
                        error = $"대화 그래프 '{container.name}'의 Action 노드 '{actionData.Guid}'에 Action 데이터가 없습니다.";
                        return false;

                    case DialogueConditionNodeData conditionData when conditionData.Condition == null:
                        error = $"대화 그래프 '{container.name}'의 Condition 노드 '{conditionData.Guid}'에 Condition 데이터가 없습니다.";
                        return false;
                }

                //시작점 캐싱
                if (nodeData is DialogueEntryNodeData entryData && !entryDataById.TryAdd(entryData.EntryId, entryData))
                {
                    error = $"대화 그래프 '{container.name}'에 중복된 진입점 ID '{entryData.EntryId}'가 있습니다.";
                    return false;
                }

                if (nodeData is not DialogueChoiceNodeData choiceNodeData)
                {
                    continue;
                }

                if (choiceNodeData.Choices == null)
                {
                    error = $"대화 그래프 '{container.name}'의 Choice 노드 '{choiceNodeData.Guid}'의 선택지 목록이 null입니다.";
                    return false;
                }

                if (choiceNodeData.Choices.Count == 0)
                {
                    error = $"대화 그래프 '{container.name}'의 Choice 노드 '{choiceNodeData.Guid}'에 선택지가 없습니다.";
                    return false;
                }

                //선택지 포트 중복 검사
                HashSet<string> portIds = new() { DialoguePortNames.Default };
                foreach (DialogueChoiceData choiceData in choiceNodeData.Choices)
                {
                    if (choiceData == null || string.IsNullOrWhiteSpace(choiceData.PortId))
                    {
                        error = $"대화 그래프 '{container.name}'의 Choice 노드 '{choiceNodeData.Guid}'에 " +
                                "null 선택지 또는 빈 선택지 포트 ID가 있습니다.";
                        return false;
                    }

                    if (!portIds.Add(choiceData.PortId))
                    {
                        error = $"대화 그래프 '{container.name}'의 Choice 노드 '{choiceNodeData.Guid}'가 " +
                                $"중복되었거나 예약된 선택지 포트 '{choiceData.PortId}'을 사용합니다.";
                        return false;
                    }

                    if (choiceData.VisibilityCondition == null)
                    {
                        error = $"대화 그래프 '{container.name}'의 Choice 노드 '{choiceNodeData.Guid}'에 " +
                                $"선택지 Condition 데이터가 없습니다.";
                        return false;
                    }
                }
            }

            //링크 정보로 딕셔너리 만들기
            foreach (NodeLinkData linkData in container.NodeLinks)
            {
                if (linkData == null
                    || string.IsNullOrWhiteSpace(linkData.StartNodeGuid)
                    || string.IsNullOrWhiteSpace(linkData.StartPortName)
                    || string.IsNullOrWhiteSpace(linkData.TargetNodeGuid))
                {
                    error = $"대화 그래프 '{container.name}'에 불완전한 연결선이 있습니다.";
                    return false;
                }

                //출력 포트별 연결선 캐싱
                var outputKey = (linkData.StartNodeGuid, linkData.StartPortName);
                if (!linkDataByOutput.TryAdd(outputKey, linkData))
                {
                    error = $"대화 그래프 '{container.name}'의 출력 포트 " +
                            $"'{linkData.StartNodeGuid}.{linkData.StartPortName}'에 연결선이 여러 개 있습니다.";
                    return false;
                }
            }

            error = null;
            return true;
        }




        //===========================현재 대화================================

        private DialogueContainer currentContainer;
        private DialogueExecutionContext currentContext;
        private NodeBaseData currentNodeData;

        /// <summary>새 대화마다 증가하는 대화 ID 발급 번호</summary>
        private int conversationIdCounter;
        
        /// <summary>현재 실행 중인 대화의 ID</summary>
        private int activeConversationId;

        /// <summary>
        /// DialogueManager 외부 코드를 실행한 뒤에도 같은 대화인지 확인
        /// </summary>
        private bool IsCurrentConversation(int conversationId, NodeBaseData nodeData = null)
        {
            return IsConversationActive && activeConversationId == conversationId && (nodeData == null || ReferenceEquals(currentNodeData, nodeData));
        }

        //===========================실행 및 재진입 상태================================

        private bool isConversationStarting;
        private bool isNodeProcessing;
        private bool isConversationEnding;

        //===========================대기 및 UI 입력 상태================================

        private BlockKind blockKind;

        private float waitTimeLeft;
        private bool useUnscaledTime;
        private string waitSignalKey;

        private readonly List<DialogueChoiceData> visibleChoices = new();

        private int promptCounter;
        private int currentPromptId;



        //===========================대화 종료 처리================================

        /// <summary>
        /// 대화 오류 발생시 중단하는 함수
        /// </summary>
        private void FailConversation(string message)
        {
            Debug.LogError(message, currentContainer);
            EndConversation(DialogueEndReason.Faulted);
        }

        /// <summary>
        /// 현재 대화를 종료하고 사용한 상태를 정리
        /// </summary>
        private void EndConversation(DialogueEndReason reason)
        {
            if (!IsConversationActive)
            {
                return;
            }

            if (reason == DialogueEndReason.Completed)
            {
                pendingCompletionCallbacks += completionCallback;
            }

            ResetBlockingState();
            ClearConversationData();
            LastEndReason = reason;

            isConversationEnding = true;
            Action<DialogueEndReason> callbacks = ConversationEnd;

            if (callbacks != null)
            {
                foreach (Action<DialogueEndReason> callback in callbacks.GetInvocationList())
                {
                    try
                    {
                        callback.Invoke(reason);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[Dialogue] {nameof(ConversationEnd)} 콜백 실행 중 예외가 발생했습니다.\n{exception}");
                    }
                }
            }

            isConversationEnding = false;

            if (!isNodeProcessing && !isConversationStarting)
            {
                InvokePendingCompletionCallbacks();
            }
        }


        //===========================대화 완료 콜백================================

        /// <summary>대화가 정상 완료되고 정리된 뒤 실행할 콜백</summary>
        private Action completionCallback;

        /// <summary>completionCallback을 미뤄둘 곳</summary>
        private Action pendingCompletionCallbacks;

        /// <summary>대화가 완전히 정리된 이후 대기 중인 완료 콜백을 실행하는 함수</summary>
        private void InvokePendingCompletionCallbacks()
        {
            Action callbacks = pendingCompletionCallbacks;
            pendingCompletionCallbacks = null;
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
                    Debug.LogError($"[Dialogue] 콜백 실행 중 예외가 발생했습니다.\n{exception}");
                }
            }
        }


        //===========================상태 초기화 함수들 모음================================

        /// <summary>
        /// block된 상태를 해체, 사용했던 변수들 초기화
        /// </summary>
        private void ResetBlockingState()
        {
            blockKind = BlockKind.None;
            waitTimeLeft = 0f;
            useUnscaledTime = false;
            waitSignalKey = null;
            currentPromptId = 0;
            visibleChoices.Clear();
        }


        /// <summary>
        /// 대화에 사용했던 기록들을 모조리 제거, 대화가 완전히 끝났을 때 호출
        /// </summary>
        private void ClearConversationData()
        {
            currentContainer = null;
            currentContext = null;
            currentNodeData = null;
            completionCallback = null;
            activeConversationId = 0;
            nodeDataByGuid.Clear();
            linkDataByOutput.Clear();
            entryDataById.Clear();
        }

        /// <summary>
        /// 플레이 세션의 Subsystem이 다시 초기화될 때 정적 실행 상태를 정리
        /// </summary>
        private void ResetManagerState()
        {
            ResetBlockingState();
            ClearConversationData();
            LastEndReason = null;
            promptCounter = 0;
            isNodeProcessing = false;
            isConversationStarting = false;
            isConversationEnding = false;
            pendingCompletionCallbacks = null;
            conversationIdCounter++;
        }
    }
}
