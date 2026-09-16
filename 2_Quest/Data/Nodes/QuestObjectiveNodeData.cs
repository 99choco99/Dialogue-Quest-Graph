using System;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>일치하는 게임 이벤트의 누적 수치가 설정한 목표량에 도달할 때까지 기다립니다.</summary>
    [Serializable]
    public class QuestObjectiveNodeData : NodeBaseData
    {
        [SerializeField]
        private string eventKey = string.Empty;

        /// <summary>
        /// 어떤 Event에 반응해서 Objective를 변화시킬지.
        /// </summary>
        public string EventKey
        {
            get => eventKey?.Trim() ?? string.Empty;
            set => eventKey = value?.Trim() ?? string.Empty;
        }

        /// <summary>
        /// Objective를 검사할 때 사용할 id
        /// </summary>
        public int TargetId;

        /// <summary>
        /// UI 등에서 활용할 때 편리하게 쓸 id의 실물
        /// </summary>
        public UnityEngine.Object TargetReference;

        [Min(1)]
        public int RequiredAmount = 1;

        [TextArea]
        public string ObjectiveDescription;
    }
}
