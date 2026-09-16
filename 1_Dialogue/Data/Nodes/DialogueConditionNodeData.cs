using System;

namespace UniversalGraph
{
	[Serializable]
	public sealed class DialogueConditionNodeData : NodeBaseData
	{
		public MethodBindingData Condition = new();
	}
}
