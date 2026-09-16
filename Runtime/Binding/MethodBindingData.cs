using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>그래프 노드와 Attribute 메서드를 연결하는 키와 전달 인수를 저장</summary>
    [Serializable]
    public sealed class MethodBindingData
    {
        [SerializeField]
        private string key = string.Empty;

        /// <summary>실행할 Attribute 메서드를 찾는 키</summary>
        public string Key
        {
            get => key?.Trim() ?? string.Empty;
            set => key = value?.Trim() ?? string.Empty;
        }

        /// <summary>키가 입력되어 있는지. 메서드 존재 여부와 인수 유효성은 별도로 검사합니다.</summary>
        public bool HasKey => Key.Length > 0;

        /// <summary>메서드에 전달할 인수 목록</summary>
        public List<MethodArgumentData> Arguments = new();
    }
}
