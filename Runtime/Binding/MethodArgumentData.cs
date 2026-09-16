using System;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>노드에 저장되는 Attribute 메서드 파라미터 값 하나
	/// <para>그래프에서 직접 입력하는 파라미터마다 하나씩 생성</para>
    /// </summary>
    [Serializable]
	public sealed class MethodArgumentData
	{
        /// <summary>
		/// 어느 파라미터의 값인지 구분
		/// </summary>
        public string ParameterId;

        /// <summary>
		/// 메서드 파라미터 타입 변경을 감지하기 위해 저장 당시의 타입 식별자를 기록
		/// </summary>
        public string TypeSignature;

        /// <summary>
        /// 값을 문자열 형태로 저장합니다.
        /// </summary>
        public string SerializedValue;

        /// <summary>
        /// Unity 에셋 참조를 저장, 문자열로 넣을 수 없기 때문.
        /// </summary>
		public UnityEngine.Object ObjectValue;
	}
}

