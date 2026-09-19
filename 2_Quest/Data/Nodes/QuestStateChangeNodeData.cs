using System;

namespace UniversalGraph
{
	[Serializable]
	public class QuestStateChangeNodeData : NodeBaseData
	{
		public QuestState NewState = QuestState.CanComplete;
	}
}
