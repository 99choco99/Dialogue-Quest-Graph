namespace UniversalGraph
{
	/// <summary>그래프에 공개되는 Attribute 메서드가 실행 명령인지 조건식인지 구분하기 위함
	/// <para>잘못된 함수를 바인드하면 노드가 터져버리기 때문에</para>
	/// </summary>
	public enum MethodKind
	{
		Action,
		Condition
	}
}
