using System;
using UnityEngine.Scripting;

namespace UniversalGraph
{
	/// <summary>bool 반환 메서드를 그래프에서 선택할 수 있는 Dialogue Condition으로 공개</summary>
	[RequireAttributeUsages]
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public sealed class DialogueConditionAttribute : PreserveAttribute
	{
		public string Key { get; }

        /// <summary>Key의 주인인 메서드를 누가 가지고 있는지 표시</summary>
        public DialogueMethodOwner Owner { get; set; } = DialogueMethodOwner.Speaker;

		public DialogueConditionAttribute(string key)
		{
			Key = key?.Trim() ?? string.Empty;
		}
	}
}
