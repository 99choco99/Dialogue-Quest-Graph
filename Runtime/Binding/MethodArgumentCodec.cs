using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UniversalGraph
{
	/// <summary>
	/// Editor에서 적은 인수랑 Runtime에서 사용할 인수 사이를 변환하는 클래스
	/// </summary>
	public static class MethodArgumentCodec
	{
        //==================== Editor용 인수 데이터 생성====================

        /// <summary>메서드 설명 정보에 맞는 형태의 기본 인수 값을 만들기</summary>
        public static List<MethodArgumentData> CreateDefaultArgumentData(MethodDescriptor descriptor)
		{
			List<MethodArgumentData> defaultArgumentData = new();
			if (descriptor == null)
			{
				return defaultArgumentData;
			}
			foreach (MethodParameterDescriptor parameterDescriptor in descriptor.SerializedParameters)
			{
				defaultArgumentData.Add(new MethodArgumentData
				{
					ParameterId = parameterDescriptor.ParameterId,
					TypeSignature = parameterDescriptor.TypeSignature,
					SerializedValue = GetDefaultSerializedValue(parameterDescriptor),
					ObjectValue = null
				});
			}
			return defaultArgumentData;
		}

		/// <summary>
		/// 타입별 SerializeValue 의 기본값을 가져옴 
		/// </summary>
        private static string GetDefaultSerializedValue(MethodParameterDescriptor descriptor)
        {
            switch (descriptor.ArgumentKind)
            {
                case MethodArgumentKind.Boolean:
                    return "false";
                case MethodArgumentKind.Integer:
                case MethodArgumentKind.FloatingPoint:
                    return "0";
                case MethodArgumentKind.Enum:
                    {
                        Array values = Enum.GetValues(descriptor.ParameterType);
                        object value = ((values.Length > 0) ? values.GetValue(0) : Activator.CreateInstance(descriptor.ParameterType));
                        return SerializeValue(value, descriptor.ParameterType);
                    }
                default:
                    return string.Empty;
            }
        }

        /// <summary>메서드 변경시 변경되지 않은 Argument를 다시 만듬</summary>
        public static List<MethodArgumentData> RepairArguments(IReadOnlyList<MethodArgumentData> existingArguments, MethodDescriptor descriptor)
		{
			List<MethodArgumentData> repairedArgumentData = CreateDefaultArgumentData(descriptor);
			if (descriptor == null || existingArguments == null)
			{
				return repairedArgumentData;
			}
			for (int parameterIndex = 0; parameterIndex < descriptor.SerializedParameters.Count; parameterIndex++)
			{
				MethodParameterDescriptor parameterDescriptor = descriptor.SerializedParameters[parameterIndex];
				for (int i = 0; i < existingArguments.Count; i++)
				{
					MethodArgumentData candidate = existingArguments[i];
					if (DecodeArgumentData(candidate, parameterDescriptor, out _, out _))
					{
						repairedArgumentData[parameterIndex] = candidate;
						break;
					}
				}
			}
			return repairedArgumentData;
		}



        //==================== 메서드 정의를 보고 지원 타입 판별 ====================

        /// <summary>정의된 파라미터 타입에 따라 인수의 타입을 결정</summary>
        public static bool GetArgumentKind(Type type, out MethodArgumentKind kind)
        {
            if (type == typeof(string))
            {
                kind = MethodArgumentKind.String;
                return true;
            }
            if (type == typeof(bool))
            {
                kind = MethodArgumentKind.Boolean;
                return true;
            }
            if (type != null && type.IsEnum)
            {
                // EnumFlagsField가 지원하지 않는 64비트 Flags는 공통 지원 타입에서 제외합니다.
                Type underlyingType = Enum.GetUnderlyingType(type);
                if (type.IsDefined(typeof(FlagsAttribute), inherit: false)
                    && (underlyingType == typeof(long) || underlyingType == typeof(ulong)))
                {
                    kind = MethodArgumentKind.String;
                    return false;
                }
                kind = MethodArgumentKind.Enum;
                return true;
            }
            if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                kind = MethodArgumentKind.UnityObject;
                return true;
            }
            if (type == typeof(int))
            {
                kind = MethodArgumentKind.Integer;
                return true;
            }
            if (type == typeof(float))
            {
                kind = MethodArgumentKind.FloatingPoint;
                return true;
            }
            kind = MethodArgumentKind.String;
            return false;
        }

        //==================== 입력한 인수를 MethodArgumentData에 저장(Encode) ====================
        //========입력한 숫자 100을 저장용 문자열 "100"으로 바꿔 MethodArgumentData에 담음.===========


        /// <summary>에디터에서 인수 하나를 수정할 때<para></para>
        /// 에디터에서 입력한 실제 C# 값을 그래프에 저장할 ArgumentData 로 변환</summary>
        public static bool TryEncodeArgumentData(MethodArgumentData data, MethodParameterDescriptor descriptor, object value, out string error)
        {
            if (data == null)
            {
                error = "저장할 인수 데이터가 null입니다.";
                return false;
            }
            if (descriptor == null)
            {
                error = "파라미터 설명 정보가 null입니다.";
                return false;
            }
            if (descriptor.Source != MethodParameterSource.Serialized)
            {
                error = $"인수 '{descriptor.ParameterId}'는 런타임에 주입되므로 그래프에 저장할 수 없습니다.";
                return false;
            }

            //파라미터 타입 가져오기
            Type parameterType = descriptor.ParameterType;
            MethodArgumentKind argumentKind = descriptor.ArgumentKind;

            bool acceptsNull = argumentKind == MethodArgumentKind.String || argumentKind == MethodArgumentKind.UnityObject;
            if (value == null && !acceptsNull)
            {
                error = $"인수 '{descriptor.ParameterId}'에는 null을 저장할 수 없습니다.";
                return false;
            }
            if (value != null && !parameterType.IsInstanceOfType(value))
            {
                error = $"인수 '{descriptor.ParameterId}'에는 {parameterType.Name} 타입 값이 필요합니다.";
                return false;
            }
            if (value is float floatingPointValue
                && (float.IsNaN(floatingPointValue) || float.IsInfinity(floatingPointValue)))
            {
                error = $"인수 '{descriptor.ParameterId}'에는 유한한 실수만 저장할 수 있습니다.";
                return false;
            }

            //value를 string으로 변환. Object는 따로
            string serializedValue = string.Empty;
            UnityEngine.Object objectValue = null;
            if (argumentKind == MethodArgumentKind.UnityObject)
            {
                objectValue = value as UnityEngine.Object;
            }
            else
            {
                serializedValue = SerializeValue(value, parameterType);
            }

            data.ParameterId = descriptor.ParameterId;
            data.TypeSignature = descriptor.TypeSignature;
            data.SerializedValue = serializedValue;
            data.ObjectValue = objectValue;

            error = null;
            return true;
        }

        /// <summary>Unity 객체가 아닌 값을 string으로 직렬화</summary>
        private static string SerializeValue(object value, Type type)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (type.IsEnum)
            {
                Type underlyingType = Enum.GetUnderlyingType(type);
                object underlyingValue = Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
                return Convert.ToString(underlyingValue, CultureInfo.InvariantCulture);
            }
            if (value is float floatValue)
            {
                return floatValue.ToString("R", CultureInfo.InvariantCulture);
            }
            if (value is double doubleValue)
            {
                return doubleValue.ToString("R", CultureInfo.InvariantCulture);
            }
            if (value is bool booleanValue)
            {
                return booleanValue ? "true" : "false";
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        //==================== 저장된 인수를 파라미터 타입에 맞게 복원(Decode) ====================
        //==============MethodArgumentData에서 문자열 "100"을 꺼내, 파라미터가 요구하는 int 값 100으로 복원. =========

        /// <summary>MethodArgumentData 하나 복원</summary>
        public static bool DecodeArgumentData(MethodArgumentData data, MethodParameterDescriptor descriptor, out object value, out string error)
        {
            value = null;

            if (data == null || descriptor == null || data.ParameterId != descriptor.ParameterId || data.TypeSignature != descriptor.TypeSignature)
            {
                error = $"저장된 인수 '{descriptor?.ParameterId}'가 현재 메서드 시그니처와 일치하지 않습니다.";
                return false;
            }

            Type parameterType = descriptor.ParameterType;
            string text = data.SerializedValue ?? string.Empty;

            switch (descriptor.ArgumentKind)
            {
                case MethodArgumentKind.String:
                    value = text;
                    error = null;
                    return true;
                case MethodArgumentKind.Boolean:
                    {
                        if (bool.TryParse(text, out bool result))
                        {
                            value = result;
                            error = null;
                            return true;
                        }
                        break;
                    }
                case MethodArgumentKind.Integer:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int integerValue))
                    {
                        value = integerValue;
                        error = null;
                        return true;
                    }
                    break;
                case MethodArgumentKind.FloatingPoint:
                    {
                        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue) && !float.IsNaN(floatValue) && !float.IsInfinity(floatValue))
                        {
                            value = floatValue;
                            error = null;
                            return true;
                        }
                        break;
                    }
                case MethodArgumentKind.Enum:
                    if (Enum.TryParse(parameterType, text, out value))
                    {
                        error = null;
                        return true;
                    }
                    break;
                case MethodArgumentKind.UnityObject:
                    if (data.ObjectValue == (object)null || parameterType.IsInstanceOfType(data.ObjectValue))
                    {
                        value = data.ObjectValue;
                        error = null;
                        return true;
                    }
                    break;
            }
            error = $"인수 '{descriptor.ParameterId}'를 {parameterType.Name} 타입으로 변환할 수 없습니다.";
            return false;
        }


        /// <summary>모든 MethodArgumentData 복원</summary>
        public static bool TryDecodeAllArgumentData(IReadOnlyList<MethodArgumentData> arguments, MethodDescriptor descriptor, out object[] result, out string error)
        {
            result = null;

            if (descriptor == null)
            {
                error = "메서드 설명 정보가 null입니다.";
                return false;
            }

            Dictionary<string, MethodArgumentData> argumentsById = new();
            int count = arguments?.Count ?? 0;

            if (count != descriptor.SerializedParameters.Count)
            {
                error = $"'{descriptor.Key}'에는 직렬화된 인수가 {descriptor.SerializedParameters.Count}개 필요하지만 {count}개 발견되었습니다.";
                return false;
            }

            if (arguments != null)
            {
                foreach (MethodArgumentData argument in arguments)
                {
                    if (argument == null || string.IsNullOrWhiteSpace(argument.ParameterId) || !argumentsById.TryAdd(argument.ParameterId, argument))
                    {
                        error = $"'{descriptor.Key}'에 null, 빈 값 또는 중복된 파라미터 ID가 있습니다.";
                        return false;
                    }
                }
            }

            //descriptor를 통해 MethodArgumentData를 하나씩 꺼내서 decode 시킴
            result = new object[descriptor.Parameters.Count];
            foreach (MethodParameterDescriptor parameterDescriptor in descriptor.SerializedParameters)
            {
                if (!argumentsById.TryGetValue(parameterDescriptor.ParameterId, out MethodArgumentData argument))
                {
                    error = $"'{descriptor.Key}'에 인수 '{parameterDescriptor.ParameterId}'가 없습니다.";
                    return false;
                }

                if (!DecodeArgumentData(argument, parameterDescriptor, out object value, out error))
                {
                    error = "'" + descriptor.Key + "' " + error;
                    return false;
                }

                result[parameterDescriptor.ParameterIndex] = value;
            }

            error = null;
            return true;
        }


        //==================== Runtime 메서드 호출 인수 준비 ====================

        /// <summary>Dialogue 메서드를 실행할 최종 object[] 생성</summary>
        public static bool CreateDialogueRuntimeArguments(IReadOnlyList<MethodArgumentData> argumentData, DialogueMethodDescriptor descriptor, DialogueExecutionContext context, out object[] result, out string error)
		{
			return CreateRuntimeArguments(argumentData, descriptor, out result, out error, dialogueContext: context);
		}

        /// <summary>Quest 메서드를 실행할 최종 object[] 생성</summary>
        public static bool CreateQuestRuntimeArguments(IReadOnlyList<MethodArgumentData> argumentData, QuestMethodDescriptor descriptor, QuestExecutionContext context, out object[] result, out string error)
		{
			return CreateRuntimeArguments(argumentData, descriptor, out result, out error, questContext: context);
		}


        /// <summary>복원 결과에 Context를 추가해 최종 object[] 배열 완성</summary>
		private static bool CreateRuntimeArguments(
            IReadOnlyList<MethodArgumentData> argumentData,
            MethodDescriptor descriptor, 
            out object[] result, 
            out string error, 
            DialogueExecutionContext dialogueContext = null, 
            QuestExecutionContext questContext = null)
		{
            //여기서 사실상 복구 완료, context는 아직임.
			if (!TryDecodeAllArgumentData(argumentData, descriptor, out result, out error))
			{
				return false;
			}

            //Decode 완료된 Argument 목록에 context를 추가
			foreach (MethodParameterDescriptor parameterDescriptor in descriptor.Parameters)
			{
				if (parameterDescriptor.Source == MethodParameterSource.DialogueExecutionContext)
				{
					if (dialogueContext == null)
					{
						error = $"'{descriptor.Key}'를 실행하려면 DialogueExecutionContext가 필요합니다.";
						return false;
					}
					result[parameterDescriptor.ParameterIndex] = dialogueContext;
					continue;
				}

				if (parameterDescriptor.Source == MethodParameterSource.QuestExecutionContext)
				{
					if (questContext == null)
					{
						error = $"'{descriptor.Key}'를 실행하려면 QuestExecutionContext가 필요합니다.";
						return false;
					}
					result[parameterDescriptor.ParameterIndex] = questContext;
					continue;
				}
			}
			error = null;
			return true;
		}

	}
}



