using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UniversalGraph.Editor
{
    /// <summary>
    /// [GraphNodeEditor]가 붙은 시각 노드 에디터를 찾아서
    /// 각 NodeBaseData 실제 타입을 연결
    /// </summary>
    public static class GraphNodeCatalog
    {
        /// <summary>노드 하나를 설명하는 읽기 전용 정보</summary>
        public sealed class NodeDefinition
        {
            public Type ViewType { get; }       //시각 노드
            public Type DataType { get; }       // 노드 데이터
            public Type ContainerType { get; }  // Quest인지 Dialogue인지
            public string MenuPath { get; }     // 생성 경로
            internal NodeDefinition(Type viewType, Type dataType, Type containerType, string menuPath)
            {
                ViewType = viewType;
                DataType = dataType;
                ContainerType = containerType;
                MenuPath = menuPath;
            }
        }

        /// <summary>
        /// 데이터 타입으로 NodeDefinition 검색
        /// </summary>
        private static readonly Dictionary<Type, NodeDefinition> NodeDefinitionByDataType = new();
        private static readonly List<NodeDefinition> NodeCatalog = new();
        private static bool isInitialized;

        /// <summary>
        /// Unity의 자동 초기화보다 먼저 노드 등록 목록이 요청되는 경우를 대비한 안전장치
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
        }

        /// <summary>스크립트를 불러오거나 다시 컴파일했을 때 다시 초기화</summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            NodeDefinitionByDataType.Clear();
            NodeCatalog.Clear();
            isInitialized = false;

            List<NodeDefinition> candidates = new();
            IEnumerable<Type> viewTypes = TypeCache.GetTypesWithAttribute<GraphNodeEditorAttribute>()
                .OrderBy(type => type.AssemblyQualifiedName);

            foreach (Type viewType in viewTypes)
            {
                if (CreateNodeDefinition(viewType, out NodeDefinition nodeDef, out string error))
                {
                    candidates.Add(nodeDef);
                }
                else
                {
                    Debug.LogError($"해당 타입의 노드를 등록할 수 없음: {error}");
                }
            }

            foreach (IGrouping<Type, NodeDefinition> group in candidates
                         .OrderBy(candidate => candidate.MenuPath, StringComparer.OrdinalIgnoreCase)
                         .GroupBy(candidate => candidate.DataType))
            {
                //중복검사
                if (group.Count() > 1)
                {
                    Debug.LogError($"[Flow Graph] 데이터 타입 '{group.Key.FullName}'에 여러 GraphNode 화면이 등록되어 있습니다: "
                        + string.Join(", ", group.Select(nodeDef => nodeDef.ViewType.FullName)));
                    continue;
                }

                //등록
                NodeDefinition nodeDef = group.First();
                NodeCatalog.Add(nodeDef);
                NodeDefinitionByDataType.Add(nodeDef.DataType, nodeDef);
            }

            isInitialized = true;
        }

        //==================== 검색 함수 =================================

        /// <summary>주어진 그래프 컨테이너에서 사용할 수 있는 노드 타입만 반환</summary>
        public static IEnumerable<NodeDefinition> GetNodeCatalog(GraphContainer container)
        {
            EnsureInitialized();
            if (container == null)
                return Array.Empty<NodeDefinition>();

            Type currentType = container.GetType();
            return NodeCatalog.Where(nodeDefinition => nodeDefinition.ContainerType.IsAssignableFrom(currentType));
        }

        /// <summary>
        /// 뷰 타입으로 데이터 타입을 찾기. 1:1이기 때문에 가능
        /// </summary>
        private static Type FindDataTypeByViewType(Type viewType)
        {
            for (Type currentViewType = viewType; currentViewType != null; currentViewType = currentViewType.BaseType)
            {
                if (currentViewType.IsGenericType && currentViewType.GetGenericTypeDefinition() == typeof(GraphNode<>))
                {
                    return currentViewType.GetGenericArguments()[0];
                }
            }

            return null;
        }

        //======================== 생성 함수 =============================

        /// <summary>
        /// 기존 직렬화 데이터 타입에 등록된 실제 화면 노드를 만들고 데이터를 연결<para></para>
        /// 노드를 컨테이너로부터 복원할 때 사용하는 함수 
        /// </summary>
        public static GraphNode CreateNodeFromData(GraphContainer container, NodeBaseData data)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container), "노드 화면을 만들 GraphContainer가 필요합니다.");
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "화면에 표시할 노드 데이터가 필요합니다.");
            }

            EnsureInitialized();
            if (!NodeDefinitionByDataType.TryGetValue(data.GetType(), out NodeDefinition nodeDef))
            {
                throw new InvalidOperationException(
                    $"노드 데이터 타입 '{data.GetType().FullName}'에 등록된 GraphNode 화면이 없습니다.");
            }

            EnsureContainerCompatibility(container, nodeDef);
            GraphNode node = CreateView(nodeDef);


            try
            {
                node.BindNodeData(data);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"'{nodeDef.ViewType.FullName}'에 '{nodeDef.DataType.FullName}' 데이터를 바인드하지 못했습니다.", exception);
            }

            return node;
        }

        /// <summary>
        /// 생성 메뉴에서 고른 항목에 맞는 새 데이터와 화면 노드 쌍을 만들기
        /// 마우스 우클릭으로 노드를 처음 만들 때 사용하는 함수
        /// </summary>
        public static GraphNode CreateNewNode(GraphContainer container, NodeDefinition nodeDef, GraphNodeCreationContext creationContext)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container), "노드를 만들 GraphContainer가 필요합니다.");
            }

            if (nodeDef == null)
            {
                throw new ArgumentNullException(nameof(nodeDef), "생성할 노드의 정의가 필요합니다.");
            }

            EnsureInitialized();
            if (!NodeDefinitionByDataType.TryGetValue(nodeDef.DataType, out NodeDefinition current)
                || !ReferenceEquals(current, nodeDef))
            {
                throw new InvalidOperationException("요청한 노드 정의가 더 이상 활성 등록 정보가 아닙니다.");
            }

            EnsureContainerCompatibility(container, nodeDef);
            GraphNode node = CreateView(nodeDef);

            //데이터 생성
            NodeBaseData data;
            try
            {
                data = node.CreateNewData(creationContext);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"'{nodeDef.ViewType.FullName}'에서 기본 노드 데이터를 생성하지 못했습니다.", exception);
            }

            //노드랑 데이터랑 1ㄷ1 매칭 근데 서로 비어있는
            node.BindNodeData(data);
            node.SetPosition(new Rect(creationContext.Position, node.DefaultSize));
            return node;
        }

        /// <summary>시각 노드 껍데기(GraphNode)를 만드는 함수</summary>
        private static GraphNode CreateView(NodeDefinition nodeDef)
        {
            try
            {
                //아묻따 클래스의 인스턴스를 만드는 함수
                return (GraphNode)Activator.CreateInstance(nodeDef.ViewType);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"GraphNode 화면 '{nodeDef.ViewType.FullName}'의 인스턴스를 생성하지 못했습니다.", exception);
            }
        }

        /// <summary>Attribute가 붙은 시각 노드 타입을 검증하고 NodeDefinition을 생성</summary>
        private static bool CreateNodeDefinition(Type viewType, out NodeDefinition nodeDef, out string error)
        {
            nodeDef = null;
            //뷰 타입
            if (viewType.IsAbstract || viewType.ContainsGenericParameters || !typeof(GraphNode).IsAssignableFrom(viewType))
            {
                error = $"'{viewType.FullName}'은 구체적인 GraphNode 클래스여야 합니다.";
                return false;
            }

            if (viewType.GetConstructor(Type.EmptyTypes) == null)
            {
                error = $"'{viewType.FullName}'에는 public 기본 생성자가 필요합니다.";
                return false;
            }

            //컨테이너 타입
            GraphNodeEditorAttribute attribute = viewType.GetCustomAttribute<GraphNodeEditorAttribute>(false);
            Type containerType = attribute.ContainerType;
            if (containerType == null || !typeof(GraphContainer).IsAssignableFrom(containerType))
            {
                error = $"'{viewType.FullName}'은 GraphContainer와 호환되는 컨테이너 타입으로 선언해야 합니다.";
                return false;
            }

            //생성 경로
            string menuPath = attribute.MenuPath;
            if (!IsValidMenuPath(menuPath))
            {
                error = $"'{viewType.FullName}'의 메뉴 경로 '{menuPath ?? "null"}'가 올바르지 않습니다.";
                return false;
            }

            //데이터 타입
            Type dataType = FindDataTypeByViewType(viewType);
            if (dataType == null)
            {
                error = $"'{viewType.FullName}'에서 구체적인 NodeBaseData 타입을 확인할 수 없습니다.";
                return false;
            }


            nodeDef = new NodeDefinition(viewType, dataType, containerType, menuPath);
            error = null;
            return true;
        }


        //========================= 노드 유효성 검사 함수 ============================
        private static void EnsureContainerCompatibility(GraphContainer container, NodeDefinition definition)
        {
            if (!definition.ContainerType.IsAssignableFrom(container.GetType()))
            {
                throw new InvalidOperationException(
                    $"노드 '{definition.ViewType.FullName}'은 '{definition.ContainerType.FullName}' 컨테이너를 지원하지만, " +
                    $"'{container.GetType().FullName}' 컨테이너는 지원하지 않습니다.");
            }
        }


        /// <summary>
        /// 유효한 생성 경로인지
        /// </summary>
        private static bool IsValidMenuPath(string menuPath)
        {
            return !string.IsNullOrWhiteSpace(menuPath)
                   && menuPath.Split('/').All(segment => !string.IsNullOrWhiteSpace(segment));
        }

    }
}
