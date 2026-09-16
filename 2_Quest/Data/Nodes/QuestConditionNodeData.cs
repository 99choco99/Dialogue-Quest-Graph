using System;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>게임의 Controller가 제공하는 Condition 결과에 따라 Quest 흐름을 분기</summary>
    [Serializable]
    public class QuestConditionNodeData : NodeBaseData
    {
        /// <summary>Attribute가 붙은 Quest Condition에 전달할 타입 기반 인수</summary>
        public MethodBindingData Condition = new();
    }
}
