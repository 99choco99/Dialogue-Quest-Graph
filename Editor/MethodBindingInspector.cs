using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniversalGraph.Editor
{
    /// <summary>
    /// 노드 인스펙터에 메서드 선택 드롭다운과 해당 메서드의 파라미터 입력 UI 전체를 만드는 클래스
    /// </summary>
    public static class MethodBindingInspector
    {
		/// <summary>인스펙터에서 실행할 Attribute 메서드를 선택하고 인수를 입력할 수 있는 칸을 생성</summary>
        public static VisualElement Create(NodeInspectorEditHandler editHandler, string title, MethodBindingData data, IReadOnlyList<MethodDescriptor> descriptorList, Action onMethodChanged = null)
        {
            VisualElement root = new ();

            Label titleLabel = new (title);
            root.Add(titleLabel);

            if (data == null)
            {
				root.Add(new HelpBox($"'{title}' 메서드 바인딩 데이터가 없습니다.", HelpBoxMessageType.Error));
                return root;
            }

            //노드가 새거면 Empty, 아니면 불러오는거니까 노드의 것을 GetKey
            string currentKey = data.Key;

            //사용 가능한 메서드들의 키를 추출
            List<string> keys = new () { string.Empty };
            keys.AddRange(descriptorList.Where(descriptor => descriptor != null).Select(descriptor => descriptor.Key));

            //드롭다운 생성
            PopupField<string> keyField = new ("Method")
            {
                choices = keys,
                formatSelectedValueCallback = GetMethodDisplayName,
                formatListItemCallback = GetMethodDisplayName
            };
            keyField.SetValueWithoutNotify(currentKey);
            root.Add(keyField);

            //선택된 메서드의 파라미터 값을 넣을 공간 마련
            VisualElement argumentsRoot = new ();
            root.Add(argumentsRoot);

            //드롭다운으로 설정된 메서드를 노드 데이터에 set
            keyField.RegisterValueChangedCallback(change =>
            {
                string selectedKey = change.newValue ?? string.Empty;
                editHandler.ApplyDataEdit("Change method", () =>
                {
                    MethodDescriptor descriptor = FindDescriptor(selectedKey);
                    data.Key = selectedKey;
                    data.Arguments = MethodArgumentCodec.CreateDefaultArgumentData(descriptor);
                    onMethodChanged?.Invoke();
                    RefreshArgumentFields();
                });
            });

            RefreshArgumentFields();
            return root;


            //==================== Create 내부 함수 ====================

            //드롭다운에서 보여주는 방식을 정의
            string GetMethodDisplayName(string key)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return "None";
                }

                MethodDescriptor descriptor = FindDescriptor(key);
                return descriptor != null ? descriptor.DisplayName : $"<등록되지 않음> {key}";
            }

            //인수를 적을 수 있는 필드를 Refresh
            void RefreshArgumentFields()
            {
                argumentsRoot.Clear();

                string key = data.Key;
                if (!data.HasKey) { return; }
                MethodDescriptor descriptor = FindDescriptor(key);

                if (descriptor == null)
                {
                    argumentsRoot.Add(new HelpBox(
                        $"'{key}'은(는) Attribute 메서드로 등록되지 않았습니다.", HelpBoxMessageType.Error));
                    return;
                }

                if (!MethodArgumentCodec.TryDecodeAllArgumentData(data.Arguments, descriptor, out object[] values, out string error))
                {
                    argumentsRoot.Add(new HelpBox(error, HelpBoxMessageType.Error));
                    argumentsRoot.Add(new Button(() =>
                    {
                        editHandler.ApplyDataEdit("Repair method arguments", () =>
                        {
                            data.Arguments = MethodArgumentCodec.RepairArguments(data.Arguments, descriptor);
                            RefreshArgumentFields();
                        });
                    })
                    {
                        text = "인수 다시 만들기 (호환되는 값 유지)"
                    });
                    return;
                }

                //Argument 입력할 수 있는 칸 만들기
                foreach (MethodParameterDescriptor parameterDescriptor in descriptor.SerializedParameters)
                {
                    MethodArgumentData argument = data.Arguments.First(candidate => candidate.ParameterId == parameterDescriptor.ParameterId);
                    argumentsRoot.Add(MethodArgumentFieldFactory.Create(editHandler, argument, parameterDescriptor, values[parameterDescriptor.ParameterIndex]));
                }
            }

            //키로 메서드 설명서 가져오기
            MethodDescriptor FindDescriptor(string key)
            {
                return descriptorList.FirstOrDefault(descriptor => descriptor != null && descriptor.Key == key);
            }
        }

    }
}
