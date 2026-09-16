using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniversalGraph.Editor
{
    /// <summary>
    /// 인스펙터에서 메서드가 지원하는 직렬화 인수 타입에 맞는 UI Toolkit 입력 필드를 제공
    /// </summary>
    public static class MethodArgumentFieldFactory
    {
        /// <summary>인수 타입에 맞는 입력 필드를 생성</summary>
        public static VisualElement Create(NodeInspectorEditHandler editHandler, MethodArgumentData argument, MethodParameterDescriptor descriptor, object initialValue)
        {
            return descriptor.ArgumentKind switch
            {
                MethodArgumentKind.String => CreateStringField(editHandler, argument, descriptor, initialValue),
                MethodArgumentKind.Boolean => CreateBoolField(editHandler, argument, descriptor, initialValue),
                MethodArgumentKind.Integer => CreateIntField(editHandler, argument, descriptor, initialValue),
                MethodArgumentKind.FloatingPoint => CreateFloatField(editHandler, argument, descriptor, initialValue),
                MethodArgumentKind.Enum => CreateEnumField(editHandler, argument, descriptor, initialValue),
                MethodArgumentKind.UnityObject => CreateObjectField(editHandler, argument, descriptor, initialValue),
                _ => new HelpBox($"'{descriptor.ParameterType.Name}' 타입을 표시할 인스펙터 입력 요소가 없습니다.", HelpBoxMessageType.Error),
            };
        }

		/// <summary>
		/// String 필드 생성
		/// </summary>
        private static VisualElement CreateStringField(NodeInspectorEditHandler editHandler, MethodArgumentData argument,
            MethodParameterDescriptor descriptor, object initialValue)
        {
            TextField field = new (descriptor.DisplayName)
            {
                value = (string)initialValue
            };

			RegisterValueChange(field, editHandler, argument, descriptor);

            return field;
        }

        /// <summary>
        /// Int 필드 생성
        /// </summary>
        private static VisualElement CreateIntField(NodeInspectorEditHandler editHandler, MethodArgumentData argument,
            MethodParameterDescriptor descriptor, object initialValue)
        {
            IntegerField field = new (descriptor.DisplayName)
            {
                value = (int)initialValue
            };

			RegisterValueChange(field, editHandler, argument, descriptor);

            return field;
        }

		/// <summary>
		/// Float 필드 생성
		/// </summary>
        private static VisualElement CreateFloatField(NodeInspectorEditHandler editHandler, MethodArgumentData argument,
            MethodParameterDescriptor descriptor, object initialValue)
        {
            FloatField field = new (descriptor.DisplayName)
            {
                value = (float)initialValue
            };

			RegisterValueChange(field, editHandler, argument, descriptor);

            return field;
        }

        /// <summary>
        /// Bool 필드 생성
        /// </summary>
        private static VisualElement CreateBoolField(NodeInspectorEditHandler editHandler, MethodArgumentData argument,
            MethodParameterDescriptor descriptor, object initialValue)
        {
            Toggle field = new (descriptor.DisplayName)
            {
                value = (bool)initialValue
            };

			RegisterValueChange(field, editHandler, argument, descriptor);

            return field;
        }

        /// <summary>enum 인수 타입에 맞는 입력 필드를 생성</summary>
		private static VisualElement CreateEnumField(NodeInspectorEditHandler editHandler, MethodArgumentData argument,
            MethodParameterDescriptor descriptor, object initialValue)
        {
            Enum currentEnum = (Enum)initialValue;
            BaseField<Enum> field = descriptor.ParameterType.IsDefined(typeof(FlagsAttribute), inherit: false)
                ? new EnumFlagsField(descriptor.DisplayName, currentEnum) : new EnumField(descriptor.DisplayName, currentEnum);
			RegisterValueChange(field, editHandler, argument, descriptor);
			return field;
        }

        /// <summary>Unity 객체 인수 타입에 에셋만 선택할 수 있는 객체 선택기를 만들기</summary>
        private static VisualElement CreateObjectField(NodeInspectorEditHandler editHandler, MethodArgumentData argument,
            MethodParameterDescriptor descriptor, object initialValue)
        {
            ObjectField field = new (descriptor.DisplayName)
            {
                objectType = descriptor.ParameterType,
                allowSceneObjects = false,
                value = initialValue as UnityEngine.Object
            };

			RegisterValueChange(field, editHandler, argument, descriptor);

            return field;
        }


        /// <summary>입력 필드의 변경을 연결하고, 저장 실패 시 이전 표시값을 복원합니다.</summary>
        private static void RegisterValueChange<T>(BaseField<T> field, NodeInspectorEditHandler editHandler, MethodArgumentData argument, MethodParameterDescriptor descriptor)
        {
            field.RegisterValueChangedCallback(change =>
            {
                if (!ApplyArgumentChange(editHandler, argument, descriptor, change.newValue))
                {
                    field.SetValueWithoutNotify(change.previousValue);
                }
            });
        }

        /// <summary>
        /// 인수 변경을 Undo 작업으로 기록하고 저장 데이터로 Encode
        /// </summary>
		private static bool ApplyArgumentChange(NodeInspectorEditHandler editHandler, MethodArgumentData argument, MethodParameterDescriptor descriptor, object value)
		{
			bool succeeded = false;
			editHandler.ApplyDataEdit($"Change {descriptor.DisplayName}", () =>
			{
				succeeded = MethodArgumentCodec.TryEncodeArgumentData(argument, descriptor, value, out string error);
				if (!succeeded)
				{
					Debug.LogError(error);
				}
			});
			return succeeded;
		}
    }
}
