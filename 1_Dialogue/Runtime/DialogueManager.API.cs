using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>게임 코드에서 Dialogue 그래프를 제어할 때 사용하는 공개 API</summary>
    public sealed partial class DialogueManager
    {
        /// <summary>대화를 하나만 사용하는 게임을 위한 기본 실행기입니다. Tick은 게임 코드에서 호출합니다.</summary>
        public static DialogueManager Instance => instance ??= new DialogueManager();

        //============================== 대화 상태들 ============================

        /// <summary>
        /// 선택지 때메 대기중인지 아닌지
        /// </summary>
        public bool IsWaitingForChoice => blockKind == BlockKind.Choice;

        /// <summary>
        /// 현재 대화중인지 아닌지
        /// </summary>
        public bool IsConversationActive => currentContainer != null;

        /// <summary>현재 화면을 표시할 때 실행기와 함께 저장하고 같은 실행기의 Continue 또는 SelectChoice에 다시 전달할 ID입니다.</summary>
        public int CurrentPromptId => currentPromptId;


        //=========================== 데이터들 ======================

        /// <summary>현재  대사</summary>
        public DialogueLineNodeData CurrentLine =>
            blockKind == BlockKind.Line ? currentNodeData as DialogueLineNodeData : null;

        /// <summary>현재 선택지 목록</summary>
        public IReadOnlyList<DialogueChoiceData> CurrentChoices =>
            blockKind == BlockKind.Choice ? visibleChoices.ToArray() : Array.Empty<DialogueChoiceData>();

        public DialogueEndReason? LastEndReason { get; private set; }

        //===========================대화 시작/종료 알림================================

        /// <summary>대화가 시작되었음을 외부 시스템에 알립니다.</summary>
        public event Action ConversationStart;

        /// <summary>대화가 종료되었음을 종료 원인과 함께 알립니다.</summary>
        public event Action<DialogueEndReason> ConversationEnd;


        //===========================UI 표시 요청================================

        /// <summary>현재 대사를 UI에 표시하도록 요청합니다.</summary>
        public event Action<DialogueLineNodeData> ShowLine;

        /// <summary>현재 선택지 목록을 UI에 표시하도록 요청합니다.</summary>
        public event Action<IReadOnlyList<DialogueChoiceData>> ShowChoices;


        //=================================실제 API 들 ===========================================

        /// <summary>
        /// 지정한 Dialogue 그래프의 Entry에서 대화를 시작합니다.<para></para>
        /// 반환값은 요청이 정상적으로 실행되었는지를 나타내며, 시작 직후 종료된 대화도 true를 반환합니다.
        /// </summary>
        public bool StartConversation(DialogueEntryPoint entryPoint, DialogueExecutionContext context = null, Action onComplete = null)
        {
            DialogueContainer container = entryPoint.Container;

            if (IsConversationActive || isNodeProcessing || isConversationStarting || isConversationEnding)
            {
                return false;
            }

            try
            {
                GraphAssetMigrator.Migrate(container);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError($"[Dialogue] 대화를 시작하지 못했습니다. {exception.Message}", container);
                return false;
            }

            //데이터 캐싱
            if (!BuildGraphIndex(container, out string error))
            {
                Debug.LogError($"[Dialogue] 대화를 시작하지 못했습니다. {error}", container);
                return false;
            }

            //시작점 찾기
            string requestedEntryId = entryPoint.EntryId;
            if (!entryDataById.TryGetValue(requestedEntryId, out DialogueEntryNodeData entryData))
            {
                Debug.LogError($"[Dialogue] 대화 그래프 '{container.name}'에 진입점 '{requestedEntryId}'가 없습니다. ",container);
                return false;
            }

            //노드 넘기기
            if (!GetNextNode(entryData.Guid, DialoguePortNames.Next, out NodeBaseData firstNodeData, out error))
            {
                Debug.LogError($"[Dialogue] 대화를 시작하지 못했습니다. {error}", container);
                return false;
            }

            //데이터 세팅
            currentContainer = container;
            currentContext = context;
            completionCallback = onComplete;
            LastEndReason = null;

            activeConversationId = ++conversationIdCounter;

            int conversationId = activeConversationId;

            isConversationStarting = true;
            InvokeDuringConversation(ConversationStart, nameof(ConversationStart), conversationId);
            isConversationStarting = false;

            // 시작 이벤트에서 대화가 바로 종료됐다면 이벤트 처리가 끝난 지금 완료 콜백을 실행
            InvokePendingCompletionCallbacks();
            if (!IsCurrentConversation(conversationId))
            {
                return true;
            }

            currentNodeData = firstNodeData;
            RunUntilBlocked();
            return true;
        }

        /// <summary>현재 대화를 정상 완료하고 완료 콜백을 호출</summary>
        public void CompleteConversation()
        {
            EndConversation(DialogueEndReason.Completed);
        }

        /// <summary>완료 콜백을 호출하지 않고 현재 대화를 취소</summary>
        public void CancelConversation()
        {
            EndConversation(DialogueEndReason.Cancelled);
        }

        /// <summary>
        /// 현재 떠 있는 대사 대기창을 다음 대사로 넘겨주는 함수
        /// </summary>
        public bool ContinueDialogue(int promptId)
        {
            if (blockKind != BlockKind.Line || promptId != currentPromptId)
            {
                return false;
            }

            ProceedToNextNode(DialoguePortNames.Next);
            return true;
        }

        /// <summary>선택지에서 선택했을 때</summary>
        public bool SelectChoice(int promptId, DialogueChoiceData choiceData)
        {
            if (blockKind != BlockKind.Choice || choiceData == null || promptId != currentPromptId)
            {
                return false;
            }

            DialogueChoiceData selectedChoiceData = visibleChoices.Find(candidateData => ReferenceEquals(candidateData, choiceData));
            if (selectedChoiceData == null)
            {
                return false;
            }

            ProceedToNextNode(selectedChoiceData.PortName);
            return true;
        }

        /// <summary>현재 Wait Signal 노드가 기다리는 신호를 보냅니다.</summary>
        public bool SendSignal(string signalKey)
        {
            if (string.IsNullOrWhiteSpace(signalKey))
            {
                return false;
            }

            string key = signalKey.Trim();
            if (blockKind != BlockKind.Signal || waitSignalKey != key)
            {
                return false;
            }

            ProceedToNextNode(DialoguePortNames.Next);
            return true;
        }

        /// <summary>Wait 노드처럼 노드에서 시간을 계산할 때 쓰는 함수<para></para>
        /// 게임 코드가 실행기마다 프레임당 한 번 호출합니다. 호출하지 않으면 시간 대기는 진행하지 않습니다.
        /// 현재 Wait가 선택한 시간만 사용하며, 남은 시간은 다음 Wait로 넘기지 않습니다.</summary>
        public void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            if (blockKind != BlockKind.Time)
            {
                return;
            }

            float deltaTime = useUnscaledTime ? unscaledDeltaTime : scaledDeltaTime;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                return;
            }

            waitTimeLeft -= deltaTime;
            if (waitTimeLeft <= 0f)
            {
                ProceedToNextNode(DialoguePortNames.Next);
            }
        }
    }
}
