using System;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>노드에 도달하면 프로젝트에서 정의한 Quest Action을 실행</summary>
    [Serializable]
    public class QuestActionNodeData : NodeBaseData
    {
        /// <summary>Attribute가 붙은 Quest Action에 전달할 타입 기반 인수</summary>
        public MethodBindingData Action = new();
    }
}
