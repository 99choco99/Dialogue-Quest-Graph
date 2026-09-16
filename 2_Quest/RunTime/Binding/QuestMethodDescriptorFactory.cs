using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace UniversalGraph
{
    /// <summary>Attribute가 붙은 Quest 메서드 시그니처를 검증하고 호출 정보를 만듭니다.</summary>
    public static class QuestMethodDescriptorFactory
    {
        /// <summary>Attribute가 붙은 Quest 메서드 하나를 검증하고 에디터, 런타임 호출 정보를 만드는 클래스</summary>
        public static bool CreateDescriptor(MethodInfo method, MethodKind kind, string key, QuestMethodOwner owner, out QuestMethodDescriptor descriptor, out string error)
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

            if (owner != QuestMethodOwner.Controller && owner != QuestMethodOwner.Global)
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

            if (owner == QuestMethodOwner.Global && !method.IsStatic)
            {
                error = $"Global 대상 '{key}' ({name})는 static 메서드여야 합니다.";
                return false;
            }

            if (owner != QuestMethodOwner.Global
                && (method.IsStatic || !typeof(IQuestController).IsAssignableFrom(method.DeclaringType)))
            {
                error = $"{owner} 대상 '{key}' ({name})는 IQuestController의 인스턴스 메서드여야 합니다.";
                return false;
            }

            //파라미터들 가져오기
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

                if (parameterType == typeof(QuestExecutionContext))
                {
                    if (hasContext)
                    {
                        error = $"'{key}' ({name})는 QuestExecutionContext를 한 번만 받을 수 있습니다.";
                        return false;
                    }

                    hasContext = true;
                    parameters[index] = new MethodParameterDescriptor(
                        index,
                        displayName,
                        displayName,
                        parameterType,
                        MethodParameterSource.QuestExecutionContext,
                        MethodArgumentKind.String);
                    continue;
                }

                //인수타입 결정
                if (!MethodArgumentCodec.GetArgumentKind(parameterType, out MethodArgumentKind argumentKind))
                {
                    error = $"'{key}' ({name})의 파라미터 '{displayName}' 타입 '{parameterType.FullName}'은 그래프 코덱에서 지원하지 않습니다.";
                    return false;
                }

                string parameterId = $"arg{serializedParameterCount++}";

                parameters[index] = new MethodParameterDescriptor(
                    index,
                    parameterId,
                    displayName,
                    parameterType,
                    MethodParameterSource.Serialized,
                    argumentKind);
            }

            //최종 설명서 제작
            descriptor = new QuestMethodDescriptor(key, kind, owner, method, parameters);
            error = null;
            return true;
        }

    }
}
