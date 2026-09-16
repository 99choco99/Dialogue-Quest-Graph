namespace UniversalGraph.Editor
{
    /// <summary>그래프 작성 단계 진단에서 사용하는 문제 심각도</summary>
    public enum GraphValidationSeverity
    {
        Warning,
        Error
    }

    /// <summary>그래프 에셋에서 발견한 작성 문제 하나하나의 단위</summary>
    public sealed class GraphValidationIssue
    {
        public GraphValidationIssue(GraphValidationSeverity severity, string issueKind, string message, string nodeGuid = null)
        {
            Severity = severity;
            IssueKind = string.IsNullOrWhiteSpace(issueKind) ? "GRAPH" : issueKind.Trim();
            Message = message ?? string.Empty;
            NodeGuid = nodeGuid;
        }

        /// <summary>
        /// 문제의 심각도
        /// </summary>
        public GraphValidationSeverity Severity { get; }

        /// <summary>
        /// 어떤 종류의 문제인가?
        /// </summary>
        public string IssueKind { get; }

        /// <summary>
        /// 사용자에게 무엇이라고 설명할 것인가?
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 어느 노드에서 발생했는가?
        /// </summary>
        public string NodeGuid { get; }

        /// <summary>Console과 간단한 진단 화면에 표시할 문자열로 문제를 변환</summary>
        public override string ToString()
        {
            string severityLabel = Severity == GraphValidationSeverity.Error ? "오류" : "경고";
            return $"[{severityLabel}] {IssueKind}: {Message}";
        }
    }
}
