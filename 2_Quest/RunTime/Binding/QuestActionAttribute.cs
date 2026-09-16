using System;
using UnityEngine.Scripting;

namespace UniversalGraph
{
    /// <summary>void 메서드를 그래프에서 선택할 수 있는 Quest Action으로 공개</summary>
    [RequireAttributeUsages]
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class QuestActionAttribute : PreserveAttribute
    {
        public QuestActionAttribute(string key)
        {
            Key = key?.Trim() ?? string.Empty;
        }

        public string Key { get; }
        public QuestMethodOwner Owner { get; set; } = QuestMethodOwner.Controller;
    }
}
