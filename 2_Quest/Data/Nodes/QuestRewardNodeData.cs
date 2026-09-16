using System;

namespace UniversalGraph
{
	[Serializable]
	public class QuestRewardNodeData : NodeBaseData
	{
		/// <summary>보상을 지급할 선택적인 Attribute 기반 Action과 인수입니다.</summary>
		public MethodBindingData RewardAction = new();
	}
}
