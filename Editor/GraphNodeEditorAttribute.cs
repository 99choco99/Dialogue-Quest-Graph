using System;

namespace UniversalGraph.Editor
{
	/// <summary>사용할 노드 클래스에 어트리뷰트를 선언해 메뉴에 띄우기 위함</summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class GraphNodeEditorAttribute : Attribute
	{
		public Type ContainerType { get; }

		public string MenuPath { get; }

		/// <summary>지정한 컨테이너 타입과 그 하위 타입에서 사용할 노드 등록, 생성 주소까지 설정</summary>
		public GraphNodeEditorAttribute(Type containerType, string menuPath)
		{
			ContainerType = containerType;
			MenuPath = menuPath?.Trim() ?? string.Empty;
		}
	}
}
