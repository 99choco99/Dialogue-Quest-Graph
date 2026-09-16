using System.Reflection;

namespace UniversalGraph
{
	/// <summary>
	/// Dialogue에서 Attribute가 붙은 메소드의 정보의 정의를 담는 클래스(읽기전용)
	/// </summary>
	public sealed class DialogueMethodDescriptor : MethodDescriptor
	{
		public DialogueMethodOwner Owner { get; }

		/// <summary>
		/// Reflection으로 얻은 MethodInfo와 파라미터 설명서로 구성
		/// </summary>
		internal DialogueMethodDescriptor(string key, MethodKind kind, DialogueMethodOwner owner, MethodInfo methodInfo, MethodParameterDescriptor[] parameters)
			: base(key, kind, methodInfo, parameters)
		{
			Owner = owner;
			DisplayName = $"{Key}  [{Owner}]  {DeclaringType.Name}.{MethodName}";
		}
	}
}
