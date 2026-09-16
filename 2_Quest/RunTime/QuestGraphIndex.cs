using System;
using System.Collections.Generic;

namespace UniversalGraph
{
    /// <summary>
    /// Quest 그래프의 노드와 연결을 한 번 정리하여 실행 중 빠르게 찾기 위한 조회 데이터
    /// </summary>
    internal sealed class QuestGraphIndex
    {
        public Dictionary<string, NodeBaseData> Nodes { get; } = new();
        public Dictionary<string, List<NodeLinkData>> OutputLinksByStartNode { get; } = new();
        public Dictionary<string, int> StartNodeCountByTargetNode { get; } = new();

        /// <summary>그래프 구조를 검사하면서 모든 런타임 인덱스를 만들기</summary>
        public QuestGraphIndex(QuestContainer container)
        {
            if (container.Nodes == null)
            {
                throw new InvalidOperationException($"'{container.name}'의 노드 목록이 null입니다.");
            }

            if (container.NodeLinks == null)
            {
                throw new InvalidOperationException($"'{container.name}'의 연결선 목록이 null입니다.");
            }

            //노드 하나씩 검사
            foreach (NodeBaseData nodeData in container.Nodes)
            {
                if (nodeData == null)
                {
                    throw new InvalidOperationException($"'{container.name}'에 null 노드가 있습니다.");
                }

                if (string.IsNullOrWhiteSpace(nodeData.Guid))
                {
                    throw new InvalidOperationException($"'{container.name}'의 {nodeData.GetType().Name} 노드에 GUID가 없습니다.");
                }

                if (!Nodes.TryAdd(nodeData.Guid, nodeData))
                {
                    throw new InvalidOperationException($"'{container.name}'에 중복된 노드 GUID '{nodeData.Guid}'가 있습니다.");
                }

                if (nodeData is QuestObjectiveNodeData objectiveData && objectiveData.RequiredAmount < 1)
                {
                    throw new InvalidOperationException($"'{container.name}'의 목표 노드 '{nodeData.Guid}' 수량은 1 이상이어야 합니다.");
                }

                if (nodeData is QuestStateWaitNodeData stateWaitData && stateWaitData.RequiredState == QuestState.ExecutionError)
                {
                    throw new InvalidOperationException($"'{container.name}'의 대기 노드 '{nodeData.Guid}'는 ExecutionError를 기다릴 수 없습니다.");
                }

                if (nodeData is QuestActionNodeData actionData && actionData.Action == null)
                {
                    throw new InvalidOperationException($"'{container.name}'의 Action 노드 '{nodeData.Guid}'에 호출 정보가 없습니다.");
                }

                if (nodeData is QuestConditionNodeData conditionData && conditionData.Condition == null)
                {
                    throw new InvalidOperationException($"'{container.name}'의 Condition 노드 '{nodeData.Guid}'에 호출 정보가 없습니다.");
                }

                if (nodeData is QuestRewardNodeData rewardData && rewardData.RewardAction == null)
                {
                    throw new InvalidOperationException($"'{container.name}'의 Reward 노드 '{nodeData.Guid}'에 호출 정보가 없습니다.");
                }
            }

            HashSet<(string SourceGuid, string SourcePort, string TargetGuid, string TargetPort)> edgeKeys = new();
            Dictionary<string, HashSet<string>> StartNodeByTargetNode = new ();

            foreach (NodeLinkData link in container.NodeLinks)
            {
                if (link == null)
                {
                    throw new InvalidOperationException($"'{container.name}'에 null 연결선이 있습니다.");
                }

                if (string.IsNullOrWhiteSpace(link.StartNodeGuid)
                    || string.IsNullOrWhiteSpace(link.TargetNodeGuid))
                {
                    throw new InvalidOperationException($"'{container.name}'에 출발 또는 도착 노드 GUID가 없는 연결선이 있습니다.");
                }

                if (string.IsNullOrWhiteSpace(link.StartPortName) || string.IsNullOrWhiteSpace(link.TargetPortName))
                {
                    throw new InvalidOperationException($"'{container.name}'에 출발 또는 도착 포트 ID가 없는 연결선이 있습니다.");
                }

                if (!Nodes.ContainsKey(link.StartNodeGuid) || !Nodes.ContainsKey(link.TargetNodeGuid))
                {
                    throw new InvalidOperationException(
                        $"'{container.name}'의 연결선이 존재하지 않는 노드를 참조합니다: " +
                        $"{link.StartNodeGuid} -> {link.TargetNodeGuid}.");
                }

                var edgeKey = (link.StartNodeGuid, link.StartPortName, link.TargetNodeGuid, link.TargetPortName);
                if (!edgeKeys.Add(edgeKey))
                {
                    throw new InvalidOperationException(
                        $"'{container.name}'에 중복된 연결선이 있습니다: " +
                        $"{link.StartNodeGuid}.{link.StartPortName} -> " +
                        $"{link.TargetNodeGuid}.{link.TargetPortName}.");
                }

                //다음에 실행할 노드들을 찾는 용도
                if (!OutputLinksByStartNode.TryGetValue(link.StartNodeGuid, out List<NodeLinkData> links))
                {
                    links = new List<NodeLinkData>();
                    OutputLinksByStartNode.Add(link.StartNodeGuid, links);
                }
                links.Add(link);

                //해당 노드로 들어오는 출발 노드들을 중복 없이 모으는 용도
                if (!StartNodeByTargetNode.TryGetValue(link.TargetNodeGuid, out HashSet<string> sources))
                {
                    sources = new HashSet<string>();
                    StartNodeByTargetNode.Add(link.TargetNodeGuid, sources);
                }

                sources.Add(link.StartNodeGuid);
            }

            //AND Gate가 기다려야 하는 입력 개수
            foreach (KeyValuePair<string, HashSet<string>> pair in StartNodeByTargetNode)
            {
                StartNodeCountByTargetNode.Add(pair.Key, pair.Value.Count);
            }
        }

    }
}
