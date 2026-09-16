using System;
using System.Collections.Generic;
using System.Reflection;

namespace UniversalGraph
{
    /// <summary>Attribute가 붙은 메서드 한 개의 공통 설명서</summary>
    public abstract class MethodDescriptor
    {
        protected MethodDescriptor(string key, MethodKind kind, MethodInfo method, MethodParameterDescriptor[] parameters)
        {
            Key = key;
            Kind = kind;
            DeclaringType = method.DeclaringType;
            MethodName = method.Name;
            IsStatic = method.IsStatic;
            MethodInfo = method;
            Parameters = parameters ?? Array.Empty<MethodParameterDescriptor>();

            List<MethodParameterDescriptor> serializedParameters = new ();
            foreach (MethodParameterDescriptor descriptor in Parameters)
            {
                if (descriptor.Source == MethodParameterSource.Serialized)
                {
                    serializedParameters.Add(descriptor);
                }
            }
            SerializedParameters = serializedParameters;

            DisplayName = $"{Key}  {DeclaringType?.Name}.{MethodName}";
        }

        //================================ 메서드 식별(어떤 메서드인지) =====================================
        /// <summary>메서드를 찾기 위한 고유 키</summary>
        public string Key { get; }

        /// <summary>메서드가 Action인지 Condition인지 구분</summary>
        public MethodKind Kind { get; }


        //================================ 원본 메서드 정보 =====================================

        /// <summary>메서드가 선언된 클래스 타입</summary>
        public Type DeclaringType { get; }

        /// <summary>메서드 이름</summary>
        public string MethodName { get; }

        /// <summary>static 메서드인지?<para> owner 가 global 인지를 판별하는 주요 bool값</prara></summary>
        public bool IsStatic { get; }

        //================================ 파라미터 정의 =====================================
        /// <summary>메서드의 전체 파라미터 정보</summary>
        public IReadOnlyList<MethodParameterDescriptor> Parameters { get; }

        /// <summary>그래프에서 값을 입력하고 저장할 파라미터의 설명서 목록</summary>
        public IReadOnlyList<MethodParameterDescriptor> SerializedParameters { get; }



        //================================ 호출 방식 =====================================

        /// <summary>Reflection 으로 호출</summary>
        public MethodInfo MethodInfo { get; }


        //================================ 에디터 표시용 =====================================

        /// <summary>그래프 드롭다운에서 표시될 이름</summary>
        public string DisplayName { get; protected set; }

        /// <summary>로그에 사용할 클래스 전체 이름과 메서드 이름을 반환</summary>
        public string QualifiedMethodName => $"{DeclaringType?.FullName}.{MethodName}";
    }
}
