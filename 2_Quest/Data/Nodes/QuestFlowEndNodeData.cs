using System;

namespace UniversalGraph
{
	[Serializable]
	public class QuestFlowEndNodeData : NodeBaseData
	{
		public QuestState NewState = QuestState.CanComplete;
	}
}
