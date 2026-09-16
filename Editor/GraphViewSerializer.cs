using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace UniversalGraph.Editor
{
    /// <summary>
    /// 시각적인 GraphView 캔버스와 직렬화 가능한 GraphContainer를 저장 및 로드
    /// </summary>
    public static class GraphViewSerializer
    {
        /// <summary>
        /// 현재 GraphView의 노드 위치, 데이터와 포트 연결을 GraphContainer에 기록
        /// </summary>
        public static void WriteGraphViewToContainer(UniversalGraphView view, GraphContainer container)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view), "그래프를 기록할 GraphView가 필요합니다.");
            }

            if (container == null)
            {
                throw new ArgumentNullException(nameof(container), "GraphView 상태를 기록할 GraphContainer가 필요합니다.");
            }

            GraphAssetMigrator.Migrate(container);

            List<GraphNode> nodes = view.nodes.OfType<GraphNode>().ToList();
            List<NodeLinkData> links = new();
            List<NodeBaseData> serializedNodes = new();
            HashSet<string> guids = new();
            HashSet<(string SourceGuid, string SourcePort, string TargetGuid, string TargetPort)> linkKeys = new();
            HashSet<(string NodeGuid, string PortName)> usedSingleOutputs = new();
            HashSet<(string NodeGuid, string PortName)> usedSingleInputs = new();

            //노드 검사
            foreach (GraphNode node in nodes)
            {
                if (node.Data == null)
                {
                    throw new InvalidOperationException("노드에 연결된 데이터가 없습니다.");
                }

                if (string.IsNullOrWhiteSpace(node.Data.Guid))
                {
                    throw new InvalidOperationException($"{node.Data.GetType().Name}에 노드 GUID가 없습니다.");
                }

                if (!guids.Add(node.Data.Guid))
                {
                    throw new InvalidOperationException($"캔버스에서 중복된 노드 GUID '{node.Data.Guid}'가 발견되었습니다.");
                }
            }
            
            //엣지 검사
            foreach (Edge edge in view.edges.ToList())
            {
                if (edge?.output?.node is not GraphNode sourceNode || edge.input?.node is not GraphNode targetNode)
                {
                    throw new InvalidOperationException("연결선의 양쪽 끝이 그래프 노드에 연결되어 있지 않습니다.");
                }

                if (string.IsNullOrWhiteSpace(edge.output.portName) || string.IsNullOrWhiteSpace(edge.input.portName))
                {
                    throw new InvalidOperationException("연결된 모든 그래프 포트에는 고정된 포트 이름이 있어야 합니다.");
                }

                var link = new NodeLinkData
                {
                    StartNodeGuid = sourceNode.Data.Guid,
                    StartPortName = edge.output.portName,
                    TargetNodeGuid = targetNode.Data.Guid,
                    TargetPortName = edge.input.portName
                };

                var linkKey = (link.StartNodeGuid, link.StartPortName, link.TargetNodeGuid, link.TargetPortName);
                if (!linkKeys.Add(linkKey))
                {
                    throw new InvalidOperationException(
                        $"연결선 {link.StartNodeGuid}.{link.StartPortName} -> {link.TargetNodeGuid}.{link.TargetPortName}이 중복되었습니다.");
                }

                if (edge.output.capacity == Port.Capacity.Single
                    && !usedSingleOutputs.Add((link.StartNodeGuid, link.StartPortName)))
                {
                    throw new InvalidOperationException(
                        $"출력 포트 '{link.StartNodeGuid}.{link.StartPortName}'에는 하나의 연결만 허용됩니다.");
                }

                if (edge.input.capacity == Port.Capacity.Single
                    && !usedSingleInputs.Add((link.TargetNodeGuid, link.TargetPortName)))
                {
                    throw new InvalidOperationException(
                        $"입력 포트 '{link.TargetNodeGuid}.{link.TargetPortName}'에는 하나의 연결만 허용됩니다.");
                }

                links.Add(link);
            }

            foreach (GraphNode node in nodes)
            {
                node.Data.Position = node.GetPosition().position;
                serializedNodes.Add(node.Data);
            }

            container.NodeLinks = links;
            container.Nodes = serializedNodes;
        }

        //============================ Load 함수들 =================================

        /// <summary>
        /// 직렬화 데이터로 캔버스를 복원
        /// </summary>
        public static void LoadGraph(UniversalGraphView view, GraphContainer container)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view), "그래프를 불러올 GraphView가 필요합니다.");
            }

            if (container == null)
            {
                throw new ArgumentNullException(nameof(container), "불러올 GraphContainer가 필요합니다.");
            }

            GraphAssetMigrator.Migrate(container);
            ValidateContainerData(container);

            List<GraphNode> nodes = LoadNodes(container);
            List<Edge> edges = LoadEdges(nodes, container);

            ClearGraph(view);
            foreach (GraphNode node in nodes)
            {
                view.AddElement(node);
            }

            foreach (Edge edge in edges)
            {
                view.AddElement(edge);
            }
        }

        /// <summary>교체할 데이터가 준비된 뒤 현재 노드와 연결선을 모두 제거합니다.</summary>
        private static void ClearGraph(UniversalGraphView view)
        {
            foreach (Edge edge in view.edges.ToList())
            {
                view.RemoveElement(edge);
            }

            foreach (GraphNode node in view.nodes.OfType<GraphNode>().ToList())
            {
                view.RemoveElement(node);
            }
        }

        /// <summary>각 시각 노드를 만들고 데이터를 연결</summary>
        private static List<GraphNode> LoadNodes(GraphContainer container)
        {
            List<GraphNode> nodes = new();
            foreach (NodeBaseData data in container.Nodes)
            {
                GraphNode node = GraphNodeCatalog.CreateNodeFromData(container, data);

                Rect position = node.GetPosition();
                position.position = data.Position;
                node.SetPosition(position);
                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>직렬화된 포트 이름을 찾아서 GraphView 연결선으로 복원</summary>
        private static List<Edge> LoadEdges(List<GraphNode> nodes, GraphContainer container)
        {
            List<Edge> edges = new();

            Dictionary<string,GraphNode> nodesByGuid = nodes.ToDictionary(node => node.Data.Guid);
            HashSet<string> usedSingleOutputs = new();
            HashSet<string> usedSingleInputs = new();

            foreach (NodeLinkData link in container.NodeLinks)
            {
                GraphNode sourceNode = nodesByGuid[link.StartNodeGuid];
                GraphNode targetNode = nodesByGuid[link.TargetNodeGuid];
                Port output = FindOutputPort(sourceNode, link);
                Port input = FindInputPort(targetNode, link);

                string outputKey = $"{link.StartNodeGuid}\u001F{link.StartPortName}";
                string inputKey = $"{link.TargetNodeGuid}\u001F{input.portName}";

                if (output.capacity == Port.Capacity.Single && !usedSingleOutputs.Add(outputKey))
                {
                    throw new InvalidOperationException($"출력 포트 '{link.StartNodeGuid}.{link.StartPortName}'에는 하나의 연결만 허용됩니다.");
                }

                if (input.capacity == Port.Capacity.Single && !usedSingleInputs.Add(inputKey))
                {
                    throw new InvalidOperationException($"입력 포트 '{link.TargetNodeGuid}.{input.portName}'에는 하나의 연결만 허용됩니다.");
                }

                Edge edge = new()
                {
                    output = output,
                    input = input
                };

                //양방향으로 서로를 인식
                output.Connect(edge);
                input.Connect(edge);
                edges.Add(edge);
            }

            return edges;
        }


        //============================ 포트 찾기 함수들 =================================

        /// <summary>연결 정보에 기록된 출발 출력 포트를 찾기</summary>
        private static Port FindOutputPort(GraphNode sourceNode, NodeLinkData link)
        {
            return FindPort(sourceNode.outputContainer.Children().OfType<Port>(), link.StartPortName, "출력", link);
        }

        /// <summary>연결 정보에 기록된 도착 입력 포트를 찾기</summary>
        private static Port FindInputPort(GraphNode targetNode, NodeLinkData link)
        {
            return FindPort(targetNode.inputContainer.Children().OfType<Port>(), link.TargetPortName, "입력", link);
        }

        /// <summary>포트 중 저장된 이름과 정확히 일치하는 포트 하나를 반환</summary>
        private static Port FindPort(IEnumerable<Port> candidates, string portName, string direction, NodeLinkData link)
        {
            Port[] ports = candidates.Where(port => port.portName == portName).ToArray();
            if (ports.Length != 1)
            {
                throw new InvalidOperationException(
                    $"연결선 '{link.StartNodeGuid}' -> '{link.TargetNodeGuid}'에서 이름이 '{portName}'인 {direction} 포트가 하나여야 하지만 {ports.Length}개 발견되었습니다.");
            }

            return ports[0];
        }

        //============================ 유효성 검사=================================


        /// <summary>에디터 화면 노드를 만들지 않고 확인할 수 있는 컨테이너 데이터를 검증합니다.</summary>
        private static void ValidateContainerData(GraphContainer container)
        {
            GraphValidationIssue[] errors = GraphValidator.ValidateStructure(container)
                .Where(issue => issue.Severity == GraphValidationSeverity.Error)
                .ToArray();
            if (errors.Length == 0)
            {
                return;
            }

            string details = string.Join(Environment.NewLine, errors.Select(issue => issue.ToString()));
            throw new InvalidOperationException($"'{container.name}'의 그래프 데이터가 올바르지 않아 불러올 수 없습니다.{Environment.NewLine}{details}");
        }
    }
}
