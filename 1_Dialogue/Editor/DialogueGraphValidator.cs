using System.Collections.Generic;
using System.Linq;
using UniversalGraph.Editor;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>대화를 실행하기 전에 Dialogue 그래프 작성 문제를 찾아 보고하는 클래스</summary>
    public sealed class DialogueGraphValidator : GraphValidatorBase<DialogueContainer>
    {
        /// <summary>시작점, 분기, 바인딩, 도달 가능 여부와 즉시 실행 순환을 검사</summary>
        protected override void Validate(DialogueContainer container, GraphValidationIndex index, ICollection<GraphValidationIssue> issues)
        {
            DialogueEntryNodeData[] entries = index.Nodes.OfType<DialogueEntryNodeData>().ToArray();
            if (entries.Length == 0)
            {
                AddError("DIALOGUE_NO_ENTRY", "Dialogue Entry 노드를 하나 이상 추가하세요.");
            }
            //시작 노드 id 중복검사
            foreach (IGrouping<string, DialogueEntryNodeData> duplicates in entries
                         .GroupBy(entryData => entryData.EntryId)
                         .Where(group => group.Count() > 1))
            {
                foreach (DialogueEntryNodeData entryData in duplicates)
                {
                    AddError("DIALOGUE_DUPLICATE_ENTRY", $"Entry ID '{duplicates.Key}'를 둘 이상의 Entry 노드가 사용하고 있습니다.", entryData.Guid);
                }
            }

            //노드 종류별 오류 검사
            foreach (NodeBaseData nodeData in index.Nodes)
            {
                switch (nodeData)
                {
                    case DialogueEntryNodeData entryData:
                        OutputValidation(entryData.Guid, DialoguePortNames.Next);
                        break;

                    case DialogueLineNodeData lineData:
                        if (string.IsNullOrWhiteSpace(lineData.DialogueText))
                        {
                            AddWarning("DIALOGUE_EMPTY_TEXT", "대화문이 비어 있습니다.", lineData.Guid);
                        }

                        OutputValidation(lineData.Guid, DialoguePortNames.Next);
                        break;

                    case DialogueChoiceNodeData choiceNodeData:
                        ValidateChoiceNode(choiceNodeData);
                        break;

                    case DialogueConditionNodeData conditionData:
                        ValidateMethodBinding(conditionData.Guid, MethodKind.Condition, conditionData.Condition, "condition", required: true);
                        OutputValidation(conditionData.Guid, DialoguePortNames.True);
                        OutputValidation(conditionData.Guid, DialoguePortNames.False);
                        break;

                    case DialogueActionNodeData actionData:
                        ValidateMethodBinding(actionData.Guid, MethodKind.Action, actionData.Action, "action", required: true);
                        OutputValidation(actionData.Guid, DialoguePortNames.Next);
                        break;

                    case DialogueWaitNodeData waitData:
                        if (waitData.DurationSeconds < 0f || float.IsNaN(waitData.DurationSeconds) || float.IsInfinity(waitData.DurationSeconds))
                        {
                            AddError("DIALOGUE_WAIT_DURATION", "Wait 시간은 0 이상의 유한한 값이어야 합니다.", waitData.Guid);
                        }
                        OutputValidation(waitData.Guid, DialoguePortNames.Next);
                        break;

                    case DialogueWaitSignalNodeData waitSignalData:
                        if (string.IsNullOrEmpty(waitSignalData.SignalKey))
                        {
                            AddError("DIALOGUE_SIGNAL_KEY", "Wait Signal에는 비어 있지 않은 Signal 키가 필요합니다.", waitSignalData.Guid);
                        }
                        OutputValidation(waitSignalData.Guid, DialoguePortNames.Next);
                        break;

                    case DialogueEndNodeData endData:
                        if (index.GetLinkInStartPort(endData.Guid).Count > 0)
                        {
                            AddError("DIALOGUE_END_OUTPUT", "End 노드에는 나가는 연결선이 있을 수 없습니다.", endData.Guid);
                        }
                        break;

                    default:
                        AddError("DIALOGUE_UNSUPPORTED_NODE", $"DialogueManager가 노드 타입 '{nodeData.GetType().Name}'을 지원하지 않습니다.", nodeData.Guid);
                        break;
                }
            }

            HashSet<string> reachable = index.GetReachableNodeGuids(entries.Select(entryData => entryData.Guid));
            if (reachable.Count > 0)
            {
                foreach (NodeBaseData nodeData in index.Nodes.Where(nodeData => !reachable.Contains(nodeData.Guid)))
                {
                    AddWarning("DIALOGUE_UNREACHABLE", "Entry에서 도달할 수 없는 노드입니다.", nodeData.Guid);
                }
            }

            foreach (string nodeGuid in index.FindCycleNodeGuids(nodeData =>
                         nodeData is DialogueActionNodeData
                         || nodeData is DialogueConditionNodeData
                         || nodeData is DialogueWaitNodeData waitData && waitData.DurationSeconds <= 0f))
            {
                AddError("DIALOGUE_IMMEDIATE_CYCLE", "이 노드는 실행을 멈출 대사, Signal 또는 양수 Wait가 없는 순환에 포함되어 있습니다.", nodeGuid);
            }


            //===========================내부 함수 ================================

            //선택지 노드가 유효한지 검사
            void ValidateChoiceNode(DialogueChoiceNodeData choiceNodeData)
            {
                if (choiceNodeData.Choices == null)
                {
                    AddError("DIALOGUE_NULL_CHOICES", "Choice 노드의 선택지 목록이 null입니다.", choiceNodeData.Guid);
                    return;
                }

                if (choiceNodeData.Choices.Count == 0)
                {
                    AddError("DIALOGUE_EMPTY_CHOICES", "Choice 노드에는 선택지를 하나 이상 추가해야 합니다.", choiceNodeData.Guid);
                    return;
                }

                //포트(선택지) 하나씩 꺼내서 검사
                HashSet<string> portIds = new () { DialoguePortNames.Default };
                for (int i = 0; i < choiceNodeData.Choices.Count; i++)
                {
                    DialogueChoiceData choice = choiceNodeData.Choices[i];
                    string label = $"선택지 {i + 1}";
                    if (choice == null || string.IsNullOrWhiteSpace(choice.PortId))
                    {
                        AddError("DIALOGUE_INVALID_CHOICE", $"{label}: 데이터 또는 포트 ID가 없습니다.", choiceNodeData.Guid);
                        continue;
                    }

                    if (!portIds.Add(choice.PortId))
                    {
                        AddError("DIALOGUE_DUPLICATE_CHOICE", $"{label}: 포트 '{choice.PortId}'이 중복되었거나 예약된 이름입니다.", choiceNodeData.Guid);
                    }

                    if (string.IsNullOrWhiteSpace(choice.ChoiceText))
                    {
                        AddWarning("DIALOGUE_EMPTY_CHOICE", $"{label}: 표시할 문장이 없습니다.", choiceNodeData.Guid);
                    }

                    ValidateMethodBinding(choiceNodeData.Guid, MethodKind.Condition, choice.VisibilityCondition, $"{label} Condition");

                    OutputValidation(choiceNodeData.Guid, choice.PortId, label);
                }

                //혹시 선택지가 없어서 default 포트를 사용하는 경우 default가 연결되어 있는지 확인
                bool needsDefault = choiceNodeData.Choices.All(choice => choice?.VisibilityCondition?.HasKey == true);

                if (needsDefault)
                {
                    OutputValidation(choiceNodeData.Guid, DialoguePortNames.Default);
                }
                else
                {
                    int defaultCount = index.GetLinkInStartPort(choiceNodeData.Guid, DialoguePortNames.Default).Count;
                    if (defaultCount > 1)
                    {
                        AddError("DIALOGUE_OUTPUT_COUNT", $"{DialoguePortNames.Default}: 최대 1개 연결 가능 (현재 {defaultCount}개)", choiceNodeData.Guid);
                    }
                }

            }

            //메서드가 유효하고 인수가 메서드에 맞는지
            void ValidateMethodBinding(string nodeGuid, MethodKind kind, MethodBindingData bindingData, string label, bool required = false)
            {
                if (bindingData == null)
                {
                    AddError("DIALOGUE_BINDING_DATA", $"{label}: 바인딩 데이터가 없습니다.", nodeGuid);
                    return;
                }

                if (!bindingData.HasKey)
                {
                    if (required)
                    {
                        AddError(kind == MethodKind.Action ? "DIALOGUE_ACTION_REQUIRED" : "DIALOGUE_CONDITION_REQUIRED", $"등록된 Dialogue {kind}을 선택하세요.", nodeGuid);
                    }
                    return;
                }

                //editor영역의 메서드 설명서를 가져오기
                if (!DialogueMethodCatalog.GetMethodDescriptor(kind, bindingData.Key, out DialogueMethodDescriptor descriptor))
                {
                    AddError("DIALOGUE_MISSING_METHOD", $"{label}: 메서드 '{bindingData.Key}'가 없습니다.", nodeGuid);
                    return;
                }

                //설명서를 통해 arguments 가져오기
                if (!MethodArgumentCodec.TryDecodeAllArgumentData(bindingData.Arguments, descriptor, out _, out string error))
                {
                    AddError("DIALOGUE_ARGUMENTS", $"{label}: {error}", nodeGuid);
                }
            }


            //Dialouge에서 나가는 선은 오로지 한개여야 한다.
            void OutputValidation(string nodeGuid, string portName, string label = null)
            {
                int count = index.GetLinkInStartPort(nodeGuid, portName).Count;
                if (count != 1)
                {
                    AddError("DIALOGUE_OUTPUT_COUNT", $"{label ?? portName}: 연결 1개 필요 (현재 {count}개)", nodeGuid);
                }
            }

            //==================================== 에러 생성 함수 ==================================

            void AddError(string issueKind, string message, string nodeGuid = null)
            {
                issues.Add(new GraphValidationIssue(GraphValidationSeverity.Error, issueKind, message, nodeGuid));
            }

            void AddWarning(string issueKind, string message, string nodeGuid = null)
            {
                issues.Add(new GraphValidationIssue(GraphValidationSeverity.Warning, issueKind, message, nodeGuid));
            }
        }
    }
}
