using System;

namespace UniversalGraph
{
	[Serializable]
	public class DialogueChoiceData
	{
		public string PortId = Guid.NewGuid().ToString();

		public string ChoiceText;

        /// <summary>이 선택지 버튼을 화면에 보여줄까 말까? (버튼 노출 조건)</summary>
        public MethodBindingData VisibilityCondition = new();
	}
}
