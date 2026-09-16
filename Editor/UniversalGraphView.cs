using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniversalGraph.Editor
{
    /// <summary>Dialogue와 Quest 그래프 에셋이 함께 사용하는 GraphView 캔버스</summary>
    public class UniversalGraphView : GraphView
    {
        //노드 관련 정보가 아닌 다른 복사 요소가 아닌지 검사하기 위함
        private const string CopyPasteHeader = "UNIVERSAL_GRAPH_CLIPBOARD_V1\n";

        private sealed class TempGraphContainer : GraphContainer { }

        private GraphContainer container;

        private int silentDepth;
        private bool changeQueued;
        private int pasteCount;
        private GraphNode lastSelectedNode;

        public event Action SaveRequest;
        public event Action<GraphNode> Selected;

        public UniversalGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            VisualElementExtensions.AddManipulator(this, new ContentDragger());         //화면을 드래그로 이동 기능 추가
            VisualElementExtensions.AddManipulator(this, new SelectionDragger());       //노드 잡고 드래그 이동
            VisualElementExtensions.AddManipulator(this, new RectangleSelector());      //드래그해서 네모 박스로 다중 선택

            // 유니티 기본 기능과 내 커스텀 함수들을 연결 (바인딩)
            graphViewChanged = HandleChange;
            serializeGraphElements = CopySelection;
            canPasteSerializedData = CanPaste;
            unserializeAndPaste = PasteSelection;

            // 모눈종이 배경 추가
            GridBackground grid = new();
            Insert(0, grid);
            grid.StretchToParentSize();
        }

        /// <summary>매개변수로 받은 그래프 노드가 선택됐음을 알리는 함수</summary>
        internal void OnNodeSelected(GraphNode node)
        {
            lastSelectedNode = node;
            Selected?.Invoke(node);
        }

        /// <summary>인스펙터가 제거된 노드 데이터를 계속 표시하지 않도록 선택 해제를 알립니다.</summary>
        internal void OnNodeUnselected(GraphNode node)
        {
            if (!ReferenceEquals(lastSelectedNode, node))
            {
                return;
            }

            lastSelectedNode = selection.OfType<GraphNode>()
                .FirstOrDefault(candidate => !ReferenceEquals(candidate, node));
            Selected?.Invoke(lastSelectedNode);
        }

        /// <summary>현재 그래프 뷰의 컨테이너를 설정</summary>
        public void SetContainer(GraphContainer container)
        {
            this.container = container;
        }


        //=============================== GraphView 변화 관련된 함수들 =====================

        private GraphViewChange HandleChange(GraphViewChange change)
        {
            if (lastSelectedNode != null && change.elementsToRemove?.Contains(lastSelectedNode) == true)
            {
                lastSelectedNode = selection.OfType<GraphNode>()
                    .FirstOrDefault(node => !change.elementsToRemove.Contains(node));
                Selected?.Invoke(lastSelectedNode);
            }

            //없어지거나, 생기거나, 이동했을 때
            bool hasChange = change.elementsToRemove != null || change.edgesToCreate != null || change.movedElements != null;
            if (silentDepth == 0 && hasChange)
            {
                ScheduleSave();
            }

            return change;
        }

        /// <summary>GraphView에 있는 편집을 하나의 Undo,저장 작업으로 합침<para>
        /// 변화 시 바로바로 저장하는게 아니라 delayCall을 줘서 프레임의 마지막에 저장하게 함.
        /// </para></summary>
        private void ScheduleSave()
        {
            if (changeQueued)
            {
                return;
            }

            changeQueued = true;
            EditorApplication.delayCall += OnSaveRequest;
        }

        /// <summary>
        /// 변화가 있을 때 프레임의 마지막에서 실행(delayCall)
        /// </summary>
        private void OnSaveRequest()
        {
            changeQueued = false;
            SaveRequest?.Invoke();
        }

        /// <summary>OnSaveRequest 없이 변화를 진행</summary>
        public void ApplyWithoutSaveRequest(Action action)
        {
            silentDepth++;
            try
            {
                action?.Invoke();
            }
            finally
            {
                silentDepth--;
            }
        }

        /// <summary>예약된 변경 요청을 취소</summary>
        public void CancelChange()
        {
            if (!changeQueued)
            {
                return;
            }

            EditorApplication.delayCall -= OnSaveRequest;
            changeQueued = false;
        }

        /// <summary>저장하거나 에셋을 바꾸기 전에 대기 중인 구조 변경을 즉시 전달</summary>
        public void FlushChange()
        {
            if (!changeQueued)
            {
                return;
            }

            EditorApplication.delayCall -= OnSaveRequest;
            OnSaveRequest();
        }


        //================================= 노드 검색 기능 함수 ====================================

        /// <summary>노드 제목, 데이터 타입명, GUID와 필드 값으로 노드를 검색</summary>
        public IReadOnlyList<GraphNode> FindNodes(string query)
        {
            string normalized = query?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return Array.Empty<GraphNode>();
            }

            return nodes.OfType<GraphNode>()
                .Where(node => BuildSearchText(node).Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .OrderBy(node => node.title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.Data?.Guid, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 노드정보로 검색용 텍스트를 생성
        /// </summary>
        private static string BuildSearchText(GraphNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            string dataJson;
            try
            {
                dataJson = node.Data == null ? string.Empty : JsonUtility.ToJson(node.Data);
            }
            catch
            {
                dataJson = string.Empty;
            }

            return $"{node.title}\n{node.DataType?.Name}\n{node.Data?.Guid}\n{dataJson}";
        }


        //================================= 노드 추가 함수 ===================================


        /// <summary>지정한 캔버스 위치에 등록된 노드 하나를 만들어 추가</summary>
        public GraphNode CreateNode(Vector2 position, GraphNodeCatalog.NodeDefinition definition)
        {
            try
            {
                if (container == null)
                {
                    throw new InvalidOperationException("노드를 만들기 전에 GraphContainer 에셋을 여세요.");
                }

                List<NodeBaseData> existingNodes = nodes.OfType<GraphNode>()
                    .Where(node => node.Data != null)
                    .Select(node => node.Data)
                    .ToList();
                GraphNodeCreationContext context = new(position, existingNodes);
                GraphNode node = GraphNodeCatalog.CreateNewNode(container, definition, context);
                AddElement(node);
                ScheduleSave();
                return node;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[Flow Graph] '{definition?.MenuPath ?? "경로를 알 수 없음"}' 노드를 생성하지 못했습니다.\n{exception}");
                return null;
            }
        }


        //================================= 노드 복사 붙여넣기 함수들 ===================================

        /// <summary>선택된 노드와 엣지들을 복사</summary>
        private string CopySelection(IEnumerable<GraphElement> elements)
        {
            GraphNode[] selected = elements?
                .OfType<GraphNode>()
                .Where(node => node.Data != null && node.capabilities.HasFlag(Capabilities.Copiable))//Entry 노드는 제외
                .Distinct() //중복 제거
                .ToArray() ?? Array.Empty<GraphNode>();

            if (selected.Length == 0)
            {
                return string.Empty;
            }

            HashSet<string> selectedIds = new(selected.Select(node => node.Data.Guid));
            TempGraphContainer copy = ScriptableObject.CreateInstance<TempGraphContainer>();
            try
            {
                copy.Nodes = selected.Select(node => node.Data).ToList();
                copy.NodeLinks = edges.ToList()
                    .Where(edge => edge?.output?.node is GraphNode source
                                   && edge.input?.node is GraphNode target
                                   && selectedIds.Contains(source.Data.Guid)
                                   && selectedIds.Contains(target.Data.Guid))
                    .Select(edge => new NodeLinkData
                    {
                        StartNodeGuid = ((GraphNode)edge.output.node).Data.Guid,
                        StartPortName = edge.output.portName,
                        TargetNodeGuid = ((GraphNode)edge.input.node).Data.Guid,
                        TargetPortName = edge.input.portName
                    }).ToList();
                return CopyPasteHeader + EditorJsonUtility.ToJson(copy);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);  //에디터용 Destroy();
            }
        }


        /// <summary>
        /// 붙여넣기 가능한지, CopyPasteHeader 상수로 시작하는지 검사하는 함수
        /// </summary>
        private bool CanPaste(string data)
        {   
            return !string.IsNullOrWhiteSpace(data) && data.StartsWith(CopyPasteHeader, StringComparison.Ordinal);
        }

        /// <summary>복사했던 것들 붙여넣기 </summary>
        private void PasteSelection(string _, string data)
        {
            if (!CanPaste(data))
            {
                return;
            }

            TempGraphContainer copy = ScriptableObject.CreateInstance<TempGraphContainer>();
            try
            {
                //앞 헤더 제거
                EditorJsonUtility.FromJsonOverwrite(data.Substring(CopyPasteHeader.Length), copy);
                if (SerializationUtility.HasManagedReferencesWithMissingTypes(copy))
                {
                    //존재하지 않는 노드 타입은 받아들이지 않음.(ex 버전 업데이트가 안되어있는 경우)
                    throw new InvalidOperationException("클립보드에 현재 사용할 수 없는 노드 타입이 있습니다.");
                }

                copy.Nodes ??= new List<NodeBaseData>();
                copy.NodeLinks ??= new List<NodeLinkData>();

                List<GraphNode> newNodes = new();
                Dictionary<string, string> newIds = new();
                Dictionary<string, GraphNode> pasted = new();
                
                Vector2 offset = Vector2.one * (30f * (pasteCount++ % 10 + 1));


                //복사된 노드를 돌면서 데이터를 채워넣음
                foreach (NodeBaseData nodeData in copy.Nodes)
                {
                    if (nodeData == null || string.IsNullOrWhiteSpace(nodeData.Guid))
                    {
                        throw new InvalidOperationException("클립보드에 null 노드 데이터 또는 빈 GUID가 있습니다.");
                    }

                    string oldId = nodeData.Guid;
                    string newId = Guid.NewGuid().ToString();

                    if (!newIds.TryAdd(oldId, newId))
                    {
                        throw new InvalidOperationException($"클립보드에 노드 GUID '{oldId}'가 중복되어 있습니다.");
                    }

                    nodeData.Guid = newId;
                    nodeData.Position += offset;

                    GraphNode node = GraphNodeCatalog.CreateNodeFromData(container, nodeData);

                    Rect rect = node.GetPosition();
                    rect.position = nodeData.Position;
                    node.SetPosition(rect);

                    newNodes.Add(node);         //완성된 노드들
                    pasted.Add(newId, node);
                }

                //붙여넣기 시 저장 요청 없이 한번에 처리
                ApplyWithoutSaveRequest(() =>
                {
                    foreach (GraphNode node in newNodes)
                    {
                        AddElement(node);
                    }

                    foreach (NodeLinkData link in copy.NodeLinks)
                    {
                        //링크중 기존 노드가 하나라도 있으면 제외
                        if (link == null
                            || !newIds.TryGetValue(link.StartNodeGuid, out string sourceId)
                            || !newIds.TryGetValue(link.TargetNodeGuid, out string targetId))
                        {
                            continue;
                        }

                        GraphNode source = pasted[sourceId];
                        GraphNode target = pasted[targetId];
                        Port output = FindPort(source.outputContainer, link.StartPortName);
                        Port input = FindPort(target.inputContainer, link.TargetPortName);
                        if (output == null || input == null
                            || output.capacity == Port.Capacity.Single && output.connected
                            || input.capacity == Port.Capacity.Single && input.connected)
                        {
                            Debug.LogWarning(
                                $"[Flow Graph] 포트를 사용할 수 없거나 이미 가득 차서 복사된 연결선 '{link.StartPortName}' -> " +
                                $"'{link.TargetPortName}'을 건너뛰었습니다.");
                            continue;
                        }

                        Edge edge = new(){ output = output, input = input };
                        output.Connect(edge);
                        input.Connect(edge);
                        AddElement(edge);
                    }
                });

                ClearSelection();
                foreach (GraphNode node in newNodes)
                {
                    AddToSelection(node);
                }
                FrameSelection();
                ScheduleSave();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Flow Graph] 복사한 노드를 붙여넣지 못했습니다.\n{exception}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy); //에디터용 Destroy();
            }
        }

        /// <summary>
        /// 요청한 포트 찾기
        /// </summary>
        private static Port FindPort(VisualElement container, string portName)
        {
            return container.Children().OfType<Port>().FirstOrDefault(port => port.portName == portName);
        }


        //================================== 오버라이드 =============================

        /// <summary>
        /// 우클릭 시 현재 컨테이너에서 생성 가능한 노드들을 보여줌
        /// 노드 생성의 첫 시작점
        /// </summary>
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            //월드좌표로 저장했다가 로컬로 바꿔서 줌아웃 문제 해결
            Vector2 worldPos = this.LocalToWorld(evt.localMousePosition);
            Vector2 canvasPos = contentViewContainer.WorldToLocal(worldPos);

            //우클릭시 노드 생성 을 추가
            foreach (GraphNodeCatalog.NodeDefinition definition in GraphNodeCatalog.GetNodeCatalog(container))
            {
                evt.menu.AppendAction(
                    definition.MenuPath,
                    _ => CreateNode(canvasPos, definition),
                    DropdownMenuAction.Status.Normal);
            }
        }

        /// <summary>포트를 서로 연결할 때 유효한지 검사하는 함수</summary>
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter _)
        {
            List<Port> compatible = new();
            foreach(Port port in ports)
            {
                bool isAlreadyConnected = startPort.connections.Any(edge =>
                    edge != null
                    && (edge.output == startPort && edge.input == port
                        || edge.output == port && edge.input == startPort));

                if (startPort.direction != port.direction
                    && startPort.node != port.node
                    && startPort.portType == port.portType
                    && !isAlreadyConnected)
                {
                    compatible.Add(port);
                }
            }
            return compatible;
        }
    }
}
