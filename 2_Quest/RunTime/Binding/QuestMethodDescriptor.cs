using System.Reflection;

namespace UniversalGraph
{
    /// <summary>
    /// Quest에서 Attribute가 붙은 메소드의 정보의 정의를 담는 클래스(읽기전용)
    /// </summary>
    public sealed class QuestMethodDescriptor : MethodDescriptor
    {
        /// <summary>
        /// Reflection으로 얻은 MethodInfo와 파라미터 설명서로 구성
        /// </summary>
        internal QuestMethodDescriptor(string key, MethodKind kind, QuestMethodOwner owner, MethodInfo method, MethodParameterDescriptor[] parameters)
            : base(key, kind, method, parameters)
        {
            Owner = owner;
            DisplayName = $"{Key}  [{Owner}]  {DeclaringType.Name}.{MethodName}";
        }

        public QuestMethodOwner Owner { get; }
    }
}
