using System;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>다른 Quest가 지정한 상태가 될 때까지 현재 흐름을 멈추는 노드의 데이터</summary>
    [Serializable]
    public class QuestStateWaitNodeData : NodeBaseData
    {
        /// <summary>
        /// 현재 흐름이 기다릴 Quest ID
        /// </summary>
        public int TargetQuestId;

        /// <summary>
        /// 대상 Quest가 이 상태가 되면 현재 흐름을 다시 진행
        /// </summary>
        public QuestState RequiredState = QuestState.TurnedIn;
    }
}
