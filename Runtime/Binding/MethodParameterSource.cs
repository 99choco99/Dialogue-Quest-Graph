namespace UniversalGraph
{
	/// <summary>메서드 인수가 기획자가 그래프에서 넣은건지 런타임에 코드로써 주입되는 값인지 구분</summary>
	public enum MethodParameterSource
	{
		Serialized,             //인스펙터에서 기획자가 입력한 값
        DialogueExecutionContext,
		QuestExecutionContext
	}
}
