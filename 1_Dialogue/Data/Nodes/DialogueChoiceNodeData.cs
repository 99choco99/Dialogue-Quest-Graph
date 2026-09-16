using System;
using System.Collections.Generic;

namespace UniversalGraph
{
    /// <summary>표시할 선택지 목록을 저장</summary>
    [Serializable]
    public sealed class DialogueChoiceNodeData : NodeBaseData
    {
        /// <summary>한 번에 함께 보여줄 선택지 목록</summary>
        public List<DialogueChoiceData> Choices = new();
    }
}
