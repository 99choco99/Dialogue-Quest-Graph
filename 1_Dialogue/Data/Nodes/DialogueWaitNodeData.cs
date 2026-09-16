using System;

namespace UniversalGraph
{
	[Serializable]
	public sealed class DialogueWaitNodeData : NodeBaseData
	{
		public float DurationSeconds = 1f;

		public bool UseUnscaledTime = true;
	}
}
