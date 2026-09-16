using UnityEngine;

namespace UniversalGraph
{
    /// <summary>
    /// Quest 정의 에셋입니다. 표시용 기본 정보와 그래프 노드에 저장된 Quest 흐름을 함께 보관합니다.
    /// </summary>
    public class QuestContainer : GraphContainer
    {
        [Header("Quest Identity")]
        public int QuestId;

        public string questName = "New Quest";

        [TextArea(3, 5)]
        public string description;

    }
}
