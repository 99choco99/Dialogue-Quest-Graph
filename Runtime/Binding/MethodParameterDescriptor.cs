using System;

namespace UniversalGraph
{
    /// <summary>Attribute 메서드 파라미터 하나에 대한 설명서</summary>
    public sealed class MethodParameterDescriptor
	{
		internal MethodParameterDescriptor(int parameterIndex, string parameterId, string displayName, Type parameterType, MethodParameterSource source, MethodArgumentKind argumentKind)
		{
			ParameterIndex = parameterIndex;
            ParameterId = parameterId;
            DisplayName = displayName;
            ParameterType = parameterType;
            Source = source;
            ArgumentKind = argumentKind;
            TypeSignature = $"{parameterType.FullName}, {parameterType.Assembly.GetName().Name}";
        }

        //================================ 파라미터 식별 =====================================
        /// <summary>파라미터가 위치한 순서</summary>
        public int ParameterIndex { get; }

		/// <summary>파라미터 ID</summary>
		public string ParameterId { get; }

        //================================ 파라미터 정의 =====================================

		/// <summary>파라미터의 실제 타입</summary>
		public Type ParameterType { get; }

        /// <summary>그래프에 저장된 인수의 타입 변경을 감지하기 위한 타입 식별자</summary>
        public string TypeSignature { get; }

        /// <summary>파라미터 값을 어떤 타입으로 처리할지 구분</summary>
        public MethodArgumentKind ArgumentKind { get; }

        //=================================== 값의 출처 ========================================

        /// <summary>파라미터 값이 코드로써 적힌 것인지, 그래프에서 적은거인지 구분</summary>
        public MethodParameterSource Source { get; }


        //=================================== 에디터 표시 ========================================
        /// <summary>에디터에 표시할 파라미터 이름</summary>
        public string DisplayName { get; }
    }
}
