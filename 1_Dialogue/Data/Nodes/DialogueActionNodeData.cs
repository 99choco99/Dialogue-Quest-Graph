using System;

namespace UniversalGraph
{
	[Serializable]
	public sealed class DialogueActionNodeData : NodeBaseData
	{
		/// <summary>
		/// 노드에 진입했을 때 실행할 Action
		/// </summary>
		public MethodBindingData Action = new();
	}
}
