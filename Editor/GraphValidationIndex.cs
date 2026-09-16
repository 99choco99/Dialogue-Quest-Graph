using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UniversalGraph.Editor
{
    /// <summary>
    /// 검증할 때 사용할 노드와 링크의 정보들을 조회하기 위해 미리 정리해둔 클래스
    /// </summary>
    public sealed class GraphValidationIndex
    {
        public GraphContainer Container { get; }
        public IReadOnlyList<NodeBaseData> Nodes { get; }
        public IReadOnlyList<NodeLinkData> Links { get; }

        private readonly Dictionary<string, NodeBaseData> nodesByGuid = new();
        private readonly Dictionary<string, List<NodeLinkData>> startLinksByGuid = new();
        private readonly Dictionary<string, List<NodeLinkData>> targetLinksByGuid = new();


        /// <summary>구조 검사를 통과한 그래프의 노드와 링크로 dictionary 제작</summary>
        public GraphValidationIndex(GraphContainer container)
        {
            Container = container != null ? container : throw new ArgumentNullException(nameof(container), "검증할 GraphContainer가 필요합니다.");
            Nodes = container.Nodes;
            Links = container.NodeLinks;

            foreach (NodeBaseData nodeData in Nodes)
            {
                nodesByGuid.Add(nodeData.Guid, nodeData);
            }

            foreach (NodeLinkData link in Links)
            {
                if (!startLinksByGuid.TryGetValue(link.StartNodeGuid, out List<NodeLinkData> Startlinks))
                {
                    Startlinks = new List<NodeLinkData>();
                    startLinksByGuid.Add(link.StartNodeGuid, Startlinks);
                }
                Startlinks.Add(link);

                if (!targetLinksByGuid.TryGetValue(link.TargetNodeGuid, out List<NodeLinkData> targetLinks))
                {
                    targetLinks = new List<NodeLinkData>();
                    targetLinksByGuid.Add(link.TargetNodeGuid, targetLinks);
                }
                targetLinks.Add(link);
            }
        }


        /// <summary>guid로 노드 데이터 가져오기</summary>
        public bool GetNodeData(string guid, out NodeBaseData nodeData)
        {
            nodeData = null;
            return !string.IsNullOrWhiteSpace(guid) && nodesByGuid.TryGetValue(guid, out nodeData);
        }

        /// <summary>출발 포트랑 연결된 링크 정보를 다 반환</summary>
        public IReadOnlyList<NodeLinkData> GetLinkInStartPort(string nodeGuid, string portName = null)
        {
            if (!startLinksByGuid.TryGetValue(nodeGuid ?? string.Empty, out List<NodeLinkData> links))
            {
                return Array.Empty<NodeLinkData>();
            }

            return string.IsNullOrWhiteSpace(portName) ? links : links.Where(link => link.StartPortName == portName).ToArray();
        }

        /// <summary>노드의 진입 포트와 연결된 링크 정보들 다 반환</summary>
        public IReadOnlyList<NodeLinkData> GetLinkInTargetPorts(string nodeGuid)
        {
            return targetLinksByGuid.TryGetValue(nodeGuid ?? string.Empty, out List<NodeLinkData> links) ? links : Array.Empty<NodeLinkData>();
        }

        /// <summary>주어진 시작 노드들에서 도달 가능한 모든 유효 노드를 찾기 BFS 사용</summary>
        public HashSet<string> GetReachableNodeGuids(IEnumerable<string> rootGuids)
        {
            HashSet<string> reachedNode = new ();

            Queue<string> q = new();
            if (rootGuids != null)
            {
                foreach (string guid in rootGuids)
                {
                    if (GetNodeData(guid, out _))
                    {
                        q.Enqueue(guid);
                    }
                }
            }

            while (q.Count > 0)
            {
                string guid = q.Dequeue();
                if (!reachedNode.Add(guid))
                {
                    continue;
                }

                foreach (NodeLinkData link in GetLinkInStartPort(guid))
                {
                    q.Enqueue(link.TargetNodeGuid);
                }
            }

            return reachedNode;
        }

        /// <summary>선택한 노드 집합 안에서 단방향 순환에 포함된 노드를 찾기 Kosaraju SCC 방식</summary>
        public HashSet<string> FindCycleNodeGuids(Func<NodeBaseData, bool> includeNode)
        {
            HashSet<string> includedGuids = new(Nodes.Where(includeNode).Select(nodeData => nodeData.Guid));
            HashSet<string> cycleNodes = new();
            Stack<string> path = new();
            Stack<string> finishOrder = new();
            Dictionary<string, int> nextLinkIndices = new();

            // Kosaraju 1단계: 정방향 DFS를 끝낸 순서대로 노드를 쌓음
            foreach (string startGuid in includedGuids)
            {
                if (nextLinkIndices.ContainsKey(startGuid))
                {
                    continue;
                }

                nextLinkIndices.Add(startGuid, 0);
                path.Push(startGuid);
                while (path.Count > 0)
                {
                    string guid = path.Peek();
                    IReadOnlyList<NodeLinkData> links = GetLinkInStartPort(guid);
                    int linkIndex = nextLinkIndices[guid];
                    if (linkIndex == links.Count)
                    {
                        finishOrder.Push(path.Pop());
                        continue;
                    }

                    // 다음 연결 번호를 기억해 자식 탐색 후 이어서 처리
                    nextLinkIndices[guid] = linkIndex + 1;
                    string nextGuid = links[linkIndex].TargetNodeGuid;
                    if (!includedGuids.Contains(nextGuid) || nextLinkIndices.ContainsKey(nextGuid))
                    {
                        continue;
                    }

                    nextLinkIndices.Add(nextGuid, 0);
                    path.Push(nextGuid);
                }
            }

            // Kosaraju 2단계: 종료 순서의 역순으로 진입 연결을 따라가면 하나의 SCC가 모임
            HashSet<string> reachedNode = new();
            List<string> component = new();
            while (finishOrder.Count > 0)
            {
                string startGuid = finishOrder.Pop();
                if (!reachedNode.Add(startGuid))
                {
                    continue;
                }

                component.Clear();
                path.Push(startGuid);
                while (path.Count > 0)
                {
                    string guid = path.Pop();
                    component.Add(guid);
                    foreach (NodeLinkData link in GetLinkInTargetPorts(guid))
                    {
                        if (includedGuids.Contains(link.StartNodeGuid) && reachedNode.Add(link.StartNodeGuid))
                        {
                            path.Push(link.StartNodeGuid);
                        }
                    }
                }

                // 서로 왕복 가능한 노드가 둘 이상이거나, 자기 자신으로 연결되면 순환
                if (component.Count > 1 || GetLinkInStartPort(startGuid).Any(link => link.TargetNodeGuid == startGuid))
                {
                    cycleNodes.UnionWith(component);
                }
            }

            return cycleNodes;
        }


    }
}
