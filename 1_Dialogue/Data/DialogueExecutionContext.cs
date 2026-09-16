using UnityEngine;

namespace UniversalGraph
{
	/// <summary>
	/// 런타임중에 Dialogue 를 위해 Speaker와 Interactor를 묶어서 관리하는 데이터박스
	/// </summary>
	public class DialogueExecutionContext
	{
		public GameObject Speaker { get; }

		public GameObject Interactor { get; }

		/// <summary>상호작용 한번에 하나씩 사용할 데이터 박스</summary>
		public DialogueExecutionContext(GameObject speaker, GameObject interactor)
		{
			Speaker = speaker;
			Interactor = interactor;
		}
	}
}
