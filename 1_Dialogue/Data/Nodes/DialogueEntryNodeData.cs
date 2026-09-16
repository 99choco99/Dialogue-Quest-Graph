using System;
using UnityEngine;

namespace UniversalGraph
{
	[Serializable]
	public sealed class DialogueEntryNodeData : NodeBaseData
	{
		public const string DefaultEntryId = "Entry";

		[SerializeField]
		private string entryId = DefaultEntryId;

		/// <summary>EntryNode의 id</summary>
		public string EntryId
		{
			get => string.IsNullOrWhiteSpace(entryId) ? DefaultEntryId : entryId.Trim();
			set => entryId = string.IsNullOrWhiteSpace(value) ? DefaultEntryId : value.Trim();
		}
	}
}
