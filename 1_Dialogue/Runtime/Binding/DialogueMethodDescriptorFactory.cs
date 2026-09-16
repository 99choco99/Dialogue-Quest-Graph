using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>이미 발견된 Dialogue Attribute 메서드가 사용 가능한지 검사하고<para>
	/// </para> DialogueMethodDescriptor로 변환하는 클래스</summary>
    public static class DialogueMethodDescriptorFactory
	{
        /// <summary>Reflection으로 찾은 Attribute 메서드 하나에 대해서 Descriptor하나 제작</summary>
        public static bool CreateDescriptor(MethodInfo method, MethodKind kind, string key, DialogueMethodOwner owner, out DialogueMethodDescriptor descriptor, out string error)
		{
			descriptor = null;
			if (method == null)
			{
				error = "MethodInfo가 null입니다.";
				return false;
			}
			if (kind != MethodKind.Action && kind != MethodKind.Condition)
			{
				error = "메서드 종류가 올바르지 않습니다.";
				return false;
			}
			if (owner != DialogueMethodOwner.Speaker && owner != DialogueMethodOwner.Interactor && owner != DialogueMethodOwner.Global)
			{
				error = $"'{key}'의 호출 대상이 올바르지 않습니다.";
				return false;
			}

			//메서드 이름 설정(오류검출용)
			string name = method.DeclaringType?.FullName + "." + method.Name;
			if (string.IsNullOrWhiteSpace(key))
			{
				error = $"'{name}'에 메서드 키가 비어 있습니다.";
				return false;
			}

			//action 메서드인가 or condition 메서드인가?
			Type expectedReturn = kind == MethodKind.Action ? typeof(void) : typeof(bool);
			if (method.ReturnType != expectedReturn)
			{
				error = $"'{key}' ({name})는 {expectedReturn.Name} 타입을 반환해야 합니다.";
				return false;
			}
			if (method.DeclaringType == null
				|| method.IsAbstract
				|| method.IsSpecialName
				|| method.IsGenericMethod
				|| method.ContainsGenericParameters)
			{
				error = $"'{key}' ({name})는 구체적인 타입에 선언된 제네릭이 아닌 구체적인 메서드여야 합니다.";
				return false;
			}
			if ((method.CallingConvention & CallingConventions.VarArgs) != 0
				|| method.IsDefined(typeof(ExtensionAttribute), inherit: false))
			{
				error = $"'{key}' ({name})는 가변 인수 또는 확장 메서드일 수 없습니다.";
				return false;
			}
			if (method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false))
			{
				error = $"'{key}' ({name})는 async 메서드일 수 없습니다.";
				return false;
			}
			if (owner == DialogueMethodOwner.Global && !method.IsStatic)
			{
				error = $"Global 대상 '{key}' ({name})는 static 메서드여야 합니다.";
				return false;
			}
			if (owner != DialogueMethodOwner.Global
				&& (method.IsStatic || !typeof(Component).IsAssignableFrom(method.DeclaringType)))
			{
				error = $"{owner} 대상 '{key}' ({name})는 Component의 인스턴스 메서드여야 합니다.";
				return false;
			}


			//모든 파라미터 가져오기
			ParameterInfo[] methodParameters = method.GetParameters();
			var parameters = new MethodParameterDescriptor[methodParameters.Length];

			int serializedParameterCount = 0;
			bool hasContext = false;
			for (int index = 0; index < methodParameters.Length; index++)
			{
				ParameterInfo parameter = methodParameters[index];

				Type parameterType = parameter.ParameterType;
				string displayName = parameter.Name ?? $"arg{index}";

				if (parameterType.IsByRef || parameter.IsOut || parameter.IsIn)
				{
					error = $"'{key}' ({name})의 파라미터 '{displayName}'에는 ref, out, in을 사용할 수 없습니다.";
					return false;
				}
				if (parameter.IsOptional || parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false))
				{
					error = $"'{key}' ({name})의 파라미터 '{displayName}'는 선택적 파라미터 또는 params일 수 없습니다.";
					return false;
				}

				//context가 있을 때
				if (parameterType == typeof(DialogueExecutionContext))
				{
					if (hasContext)
					{
						error = $"'{key}' ({name})는 DialogueExecutionContext를 한 번만 받을 수 있습니다.";
						return false;
					}
					hasContext = true;
					parameters[index] = new MethodParameterDescriptor(
						index,
						displayName,
						displayName,
						parameterType,
						MethodParameterSource.DialogueExecutionContext,
						MethodArgumentKind.String);
					continue;
				}

				//인수의 타입을 결정
				if (!MethodArgumentCodec.GetArgumentKind(parameterType, out MethodArgumentKind argumentKind))
				{
					error = $"'{key}' ({name})의 파라미터 '{displayName}' 타입 '{parameterType.FullName}'은 그래프 코덱에서 지원하지 않습니다.";
					return false;
				}

				//파라미터 설명서 완성
				string parameterId = $"arg{serializedParameterCount++}";
				parameters[index] = new MethodParameterDescriptor(
					index,
					parameterId,
					displayName,
					parameterType,
					MethodParameterSource.Serialized,
					argumentKind);
			}

			descriptor = new DialogueMethodDescriptor(key, kind, owner, method, parameters);

			error = null;
			return true;
		}
	}
}
