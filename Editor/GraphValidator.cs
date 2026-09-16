using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UniversalGraph.Editor
{
    /// <summary>그래프 하나의 공통 구조를 검사한 뒤, 해당 그래프의 전용 검증기를 호출해 결과를 합침</summary>
    public static class GraphValidator
    {
        private static readonly List<IGraphValidator> Validators = new();
        private static readonly List<GraphValidationIssue> initializationIssues = new();

        /// <summary>처음 사용할 때 검증기 목록을 만들기</summary>
        static GraphValidator()
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IGraphValidator>()
                         .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters))
            {
                try
                {
                    IGraphValidator validator = (IGraphValidator)Activator.CreateInstance(type);
                    if (validator.ContainerType == null || !typeof(GraphContainer).IsAssignableFrom(validator.ContainerType))
                    {
                        throw new InvalidOperationException("검사할 GraphContainer 타입이 필요합니다.");
                    }
                    Validators.Add(validator);
                }
                catch (Exception exception)
                {
                    initializationIssues.Add(new GraphValidationIssue(GraphValidationSeverity.Error,  "VALIDATOR_INITIALIZATION",  $"Validator '{type.FullName}' 초기화 실패: {exception.GetBaseException().Message}"));
                }
            }
        }

        /// <summary>그래프 에셋 한개에 대해서 기본적인 구조랑 데이터들 검사를 실행</summary>
        public static IReadOnlyList<GraphValidationIssue> Validate(GraphContainer container)
        {
            //구조 결함 검사
            List<GraphValidationIssue> issues = new(ValidateStructure(container));
            issues.AddRange(initializationIssues);
            if (issues.Any(issue => issue.Severity == GraphValidationSeverity.Error))
            {
                return issues;
            }

            //공통검사를 끝냈으니 이제부터 Graph의 개별적인 검사를 진행
            GraphValidationIndex index = new (container);

            Type containerType = container.GetType();
            foreach (IGraphValidator validator in Validators
                         .Where(item => item.ContainerType.IsAssignableFrom(containerType)))
            {
                try
                {
                    validator.Validate(index, issues);
                }
                catch (Exception exception)
                {
                    issues.Add(new GraphValidationIssue(GraphValidationSeverity.Error, "VALIDATOR_EXCEPTION", $"Validator '{validator.GetType().Name}' 실행에 실패했습니다: {exception.Message}"));
                }
            }

            return issues;
        }

        /// <summary>
        /// 모든 그래프에 공통인 직렬화 구조만 검사
        /// </summary>
        public static IReadOnlyList<GraphValidationIssue> ValidateStructure(GraphContainer container)
        {
            if (container == null)
            {
                return new[] { new GraphValidationIssue(GraphValidationSeverity.Error, "GRAPH_NULL", "불러온 그래프 에셋이 없습니다.") };
            }

            List<GraphValidationIssue> issues = new ();
            //버전 확인
            if (container.SchemaVersion != GraphAssetMigrator.CurrentVersion)
            {
                AddError("GRAPH_SCHEMA_VERSION", $"지원하지 않는 그래프 버전: {container.SchemaVersion} (지원: {GraphAssetMigrator.CurrentVersion})");
            }

            //컨테이너 null 검사
            if (SerializationUtility.HasManagedReferencesWithMissingTypes(container))
            {
                AddError("MISSING_NODE_TYPE", "에셋에 C# 타입이 사라진 노드 데이터가 있습니다.");
            }
            if (container.Nodes == null)
            {
                AddError("NULL_NODE_LIST", "그래프 노드 목록이 null입니다.");
                return issues;
            }

            if (container.NodeLinks == null)
            {
                AddError("NULL_LINK_LIST", "그래프 연결선 목록이 null입니다.");
                return issues;
            }

            //그래프 노드 검사
            HashSet<string> guids = new() ;
            foreach (NodeBaseData nodeData in container.Nodes)
            {
                if (nodeData == null)
                {
                    AddError("NULL_NODE", "그래프에 null 노드 항목이 있습니다.");
                }
                else if (string.IsNullOrWhiteSpace(nodeData.Guid))
                {
                    AddError("EMPTY_NODE_GUID", $"{nodeData.GetType().Name}에 고정 GUID가 없습니다.");
                }
                else if (!guids.Add(nodeData.Guid))
                {
                    AddError("DUPLICATE_NODE_GUID", $"노드 GUID '{nodeData.Guid}'가 중복되었습니다.", nodeData.Guid);
                }
            }

            //노드 사이의 링크들 검사
            HashSet<string> edgeKeys = new();
            foreach (NodeLinkData link in container.NodeLinks)
            {
                if (link == null)
                {
                    AddError("NULL_LINK", "그래프에 null 연결선 항목이 있습니다.");
                    continue;
                }

                string sourceGuid = link.StartNodeGuid;
                string targetGuid = link.TargetNodeGuid;
                if (string.IsNullOrWhiteSpace(sourceGuid)
                    || string.IsNullOrWhiteSpace(targetGuid)
                    || string.IsNullOrWhiteSpace(link.StartPortName))
                {
                    AddError("INCOMPLETE_LINK", "연결선에 출발 노드, 대상 노드 또는 출력 포트 ID가 없습니다.");
                    continue;
                }

                if (!guids.Contains(sourceGuid) || !guids.Contains(targetGuid))
                {
                    AddError("MISSING_LINK_NODE", $"연결선이 존재하지 않는 노드를 참조합니다: {sourceGuid} -> {targetGuid}.", guids.Contains(sourceGuid) ? sourceGuid : null);
                }

                if (string.IsNullOrWhiteSpace(link.TargetPortName))
                {
                    AddError("MISSING_TARGET_PORT", "연결선에 대상 입력 포트 ID가 없습니다.", sourceGuid);
                }

                //중복검사
                string edgeKey = $"{sourceGuid}\u001F{link.StartPortName}\u001F{targetGuid}\u001F{link.TargetPortName}";
                if (!edgeKeys.Add(edgeKey))
                {
                    AddError("DUPLICATE_LINK", $"연결선 {sourceGuid}.{link.StartPortName} -> {targetGuid}.{link.TargetPortName}이 중복되었습니다.", sourceGuid);
                }
            }

            return issues;


            //===================================================


            void AddError(string issueKind, string message, string nodeGuid = null)
            {
                issues.Add(new GraphValidationIssue(GraphValidationSeverity.Error, issueKind, message, nodeGuid));
            }
        }


    }
}
