using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniversalGraph.Editor
{
    /// <summary>
    /// 공통 그래프 캔버스와 노드 Inspector를 제공
    /// 에디터 저장과 Undo를 관리
    /// </summary>
    public class UniversalGraphWindow : EditorWindow
    {
        private UniversalGraphToolbar toolbar;
        private UniversalGraphView graphView;
        private NodeInspector inspectorPanel;
        private IVisualElementScheduledItem inspectorUpdate;


        private string previousSearchQuery;
        private int nextSearchIndex;
        private IReadOnlyList<GraphValidationIssue> validationIssues = Array.Empty<GraphValidationIssue>();
        private int nextIssueIndex;

        [SerializeField]
        private GraphContainer currentContainer;
        private GraphContainer loadedContainer;
        private bool isLoading;

        //========================== 그래프창 열기===========================

        /// <summary>그래프 에셋을 열기 함수</summary>
        public static void OpenWindow(GraphContainer container)
        {
            if (container == null)
            {
                Debug.LogError("Container가 비어있어서 창을 열 수 없습니다.");
                return;
            }

            UniversalGraphWindow[] windows = Resources.FindObjectsOfTypeAll<UniversalGraphWindow>();
            foreach (UniversalGraphWindow window in windows)
            {
                if (!ReferenceEquals(window.currentContainer, container))
                {
                    continue;
                }

                window.Show();  //보여주기
                window.Focus(); //선택하기
                return;
            }

            //이미 열려있는 기존 UniversalGraphWindow 옆에 새 탭으로 깔끔하게 붙여주라는 뜻
            UniversalGraphWindow newWindow = CreateWindow<UniversalGraphWindow>(typeof(UniversalGraphWindow));
            newWindow.LoadData(container);
            newWindow.UpdateWindowTitle(container);
            newWindow.Show();
            newWindow.Focus();
        }

        /// <summary>프로젝트 창에서 그래프 컨테이너 에셋을 더블 클릭해서 열기</summary>
        [OnOpenAsset(1)]
        public static bool OnOpenAsset(int entityId)
        {
            if (EditorUtility.EntityIdToObject(entityId) is GraphContainer container)
            {
                OpenWindow(container);
                return true;
            }

            return false;
        }

        //========================== 그래프창 열고 닫았을 때===========================

        private void OnEnable()
        {
            Construct();

            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);

            Undo.undoRedoPerformed += OnUndoRedo;

            if (currentContainer != null)
            {
                LoadData(currentContainer, flushPendingChanges: false);
                if (ReferenceEquals(loadedContainer, currentContainer))
                {
                    UpdateWindowTitle(currentContainer);
                }
            }

        }

        /// <summary>에디터 창에서 사용할 캔버스와 인스펙터 만들기</summary>
        private void Construct()
        {
            rootVisualElement.Clear();
            UniversalGraphStyles.AttachTo(rootVisualElement);

            //툴바 생성
            toolbar = new UniversalGraphToolbar(SelectNextIssue, SearchAndFocusNode);
            rootVisualElement.Add(toolbar);

            //캔버스 생성
            TwoPaneSplitView splitView = new(1, 500f, TwoPaneSplitViewOrientation.Horizontal);
            splitView.AddToClassList(UniversalGraphStyles.SplitViewClass);
            rootVisualElement.Add(splitView);

            //그래프 뷰 생성
            graphView = new UniversalGraphView
            {
                name = "Flow Graph",
                focusable = true
            };
            graphView.SaveRequest += OnSaveGraphChanged;
            graphView.Selected += OnNodeSelected;
            splitView.Add(graphView);

            //인스펙터 생성
            inspectorPanel = new NodeInspector(SyncDataFromInspector, SyncStructureFromInspector);
            splitView.Add(inspectorPanel);
        }

        /// <summary>
        /// window 이름 업데이트
        /// </summary>
        private void UpdateWindowTitle(GraphContainer container)
        {
            titleContent = new GUIContent($"Flow Graph - {container.name}");
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown);

            SaveAssetToDisk();
            if (graphView == null)
            {
                return;
            }

            graphView.CancelChange();
            graphView.SaveRequest -= OnSaveGraphChanged;
            graphView.Selected -= OnNodeSelected;
            graphView.RemoveFromHierarchy();
        }

        //========================== 그래프 저장 관련 함수들 ===========================

        /// <summary>
        /// ctrl + S 키 감지로 그래프 저장
        /// </summary>
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.S || !evt.actionKey)
            {
                return;
            }

            SaveAssetToDisk();
            evt.StopPropagation();
        }

        /// <summary>
        /// 인스펙터에서 필드수정 같은 일반 데이터 수정 한 번을 Undo 작업으로 적용<para></para>
        /// 포트나 연결선을 바꾸는 수정은 SyncStructureFromInspector 사용
        /// </summary>
        private void SyncDataFromInspector(string undoName, Action edit)
        {
            if (isLoading || !CanSaveLoadedGraph())
            {
                return;
            }

            graphView.FlushChange();

            Undo.RecordObject(currentContainer, undoName);
            graphView.ApplyWithoutSaveRequest(edit);
            EditorUtility.SetDirty(currentContainer);
            ValidateCurrentGraph();
        }

        /// <summary>인스펙터에서 포트나 연결선을 바꾸는 구조 수정을 하나의 Undo 작업으로 적용</summary>
        private void SyncStructureFromInspector(string undoName, Action edit)
        {
            if (isLoading || !CanSaveLoadedGraph())
            {
                return;
            }

            graphView.FlushChange();

            Undo.RegisterCompleteObjectUndo(currentContainer, undoName);
            graphView.ApplyWithoutSaveRequest(edit);
            SyncGraphViewToContainer();
        }

        /// <summary>노드 생성, 삭제, 이동과 연결 변경을 Undo에 기록하고 Container에 반영</summary>
        private void OnSaveGraphChanged()
        {
            if (isLoading || !CanSaveLoadedGraph())
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(currentContainer, "Change Graph");
            SyncGraphViewToContainer();
        }

        /// <summary>현재 GraphView 상태를 Container에 쓰고 변경 상태와 검증 결과를 갱신</summary>
        private void SyncGraphViewToContainer()
        {
            GraphViewSerializer.WriteGraphViewToContainer(graphView, currentContainer);
            EditorUtility.SetDirty(currentContainer);
            ValidateCurrentGraph();
        }

        /// <summary>현재 그래프 에셋을 디스크에 저장</summary>
        private void SaveAssetToDisk()
        {
            if (!CanSaveLoadedGraph())
            {
                return;
            }

            graphView.FlushChange();
            AssetDatabase.SaveAssetIfDirty(currentContainer);
        }

        /// <summary>그래프가 저장이 가능한 상태인지</summary>
        private bool CanSaveLoadedGraph()
        {
            return graphView != null && currentContainer != null && ReferenceEquals(loadedContainer, currentContainer);
        }


        //========================= 그래프 불러오기 및 되돌리기 함수 =================================

        /// <summary>캔버스에서 노드를 선택하면 해당 노드의 Inspector를 표시</summary>
        private void OnNodeSelected(GraphNode selectedNode)
        {
            inspectorUpdate?.Pause();

            if (inspectorPanel.focusController?.focusedElement is VisualElement focused &&
                inspectorPanel.Contains(focused))
            {
                focused.Blur(); 
            }

            inspectorUpdate = inspectorPanel.schedule.Execute(() => inspectorPanel.UpdateInspector(selectedNode));
        }

        /// <summary>
        /// Undo나 Redo 뒤 그래프를 다시 불러오고 기존 선택 노드가 남아 있으면 다시 선택
        /// </summary>
        private void OnUndoRedo()
        {
            if (currentContainer == null || graphView == null)
            {
                return;
            }

            GraphNode[] selectedNodes = graphView.selection.OfType<GraphNode>().ToArray();
            string selectedNodeGuid = selectedNodes.Length == 1 ? selectedNodes[0].Data?.Guid : null;
            graphView.CancelChange();
            LoadData(currentContainer, flushPendingChanges: false);
            RestoreSelection(selectedNodeGuid);
        }

        /// <summary>Undo나 Redo로 캔버스를 다시 만든 뒤 인스펙터 선택을 복원</summary>
        private void RestoreSelection(string nodeGuid)
        {
            if (string.IsNullOrWhiteSpace(nodeGuid))
            {
                inspectorPanel?.UpdateInspector(null);
                return;
            }

            GraphNode node = graphView.nodes.OfType<GraphNode>().FirstOrDefault(candidate => candidate.Data?.Guid == nodeGuid);
            if (node == null)
            {
                inspectorPanel?.UpdateInspector(null);
                return;
            }

            graphView.ClearSelection();
            graphView.AddToSelection(node);
        }


        /// <summary>
        /// 그래프 데이터 불러오기
        /// </summary>
        private void LoadData(GraphContainer container, bool flushPendingChanges = true)
        {
            if (container == null || graphView == null)
            {
                return;
            }

            if (flushPendingChanges && CanSaveLoadedGraph())
            {
                graphView.FlushChange();
            }
            else
            {
                graphView.CancelChange();
            }

            GraphContainer previousContainer = currentContainer;
            isLoading = true;

            try
            {
                MigrateGraphAssetIfNeeded(container);
                graphView.ApplyWithoutSaveRequest(() => GraphViewSerializer.LoadGraph(graphView, container));

                currentContainer = container;
                loadedContainer = container;
                graphView.SetContainer(container);

                inspectorPanel?.UpdateInspector(null);
                ValidateCurrentGraph();
            }
            catch (Exception exception)
            {
                currentContainer = previousContainer;
                loadedContainer = null;
                graphView.SetContainer(null);
                inspectorPanel?.UpdateInspector(null);
                Debug.LogError(
                    $"[Flow Graph] '{container.name}'의 화면을 불러오지 못했습니다. " +
                    $"마이그레이션이 실행된 경우 에셋에는 이미 반영되어 있을 수 있습니다.\n{exception}",
                    container);
            }
            finally
            {
                isLoading = false;
            }
        }


        //====================================== 유효성 검사 ==============================

        /// <summary>에디터 화면을 만들기 전에 안전한 순차 스키마 업그레이드를 저장합니다.</summary>
        private static void MigrateGraphAssetIfNeeded(GraphContainer container)
        {
            GraphAssetMigrationResult result = GraphAssetMigrator.Migrate(container);

            if (!result.Changed)
            {
                return;
            }

            EditorUtility.SetDirty(container);
            AssetDatabase.SaveAssetIfDirty(container);
            Debug.Log(
                $"[Flow Graph] '{container.name}'을 스키마 {result.BeforeVersion}에서 " +
                $"{result.AfterVersion}(으)로 마이그레이션했습니다.",
                container);
        }

        /// <summary>그래프 창으로 돌아오면 외부에서 변경된 참조를 다시 검사</summary>
        private void OnFocus()
        {
            ValidateCurrentGraph();
        }

        //==============================ToolBar 함수들 ===================================

        /// <summary>공통, 도메인 규칙을 실행하고 노드 진단 결과를 현재 캔버스에 표시합니다.</summary>
        private void ValidateCurrentGraph()
        {
            if (!CanSaveLoadedGraph())
            {
                validationIssues = Array.Empty<GraphValidationIssue>();
                toolbar?.UpdateValidationDisplay(validationIssues);
                return;
            }

            validationIssues = GraphValidator.Validate(currentContainer);
            var issuesByNode = validationIssues
                .Where(issue => !string.IsNullOrWhiteSpace(issue.NodeGuid))
                .GroupBy(issue => issue.NodeGuid)
                .ToDictionary(group => group.Key, group => group.AsEnumerable());

            foreach (GraphNode node in graphView.nodes.OfType<GraphNode>())
            {
                node.SetValidationIssues(
                    node.Data != null && issuesByNode.TryGetValue(node.Data.Guid, out IEnumerable<GraphValidationIssue> issues)
                        ? issues
                        : Array.Empty<GraphValidationIssue>());
            }

            nextIssueIndex = 0;
            toolbar?.UpdateValidationDisplay(validationIssues);
            inspectorPanel?.RefreshValidationDisplay();
        }

        /// <summary>검증 문제가 있는 다음 노드를 선택하고 화면 중앙에 표시합니다.</summary>
        private void SelectNextIssue()
        {
            GraphValidationIssue[] nodeIssues = validationIssues
                .Where(issue => !string.IsNullOrWhiteSpace(issue.NodeGuid))
                .ToArray();
            if (nodeIssues.Length == 0)
            {
                return;
            }

            GraphValidationIssue issue = nodeIssues[nextIssueIndex++ % nodeIssues.Length];
            GraphNode node = graphView.nodes.OfType<GraphNode>()
                .FirstOrDefault(candidate => candidate.Data?.Guid == issue.NodeGuid);
            if (node == null)
            {
                return;
            }

            graphView.ClearSelection();
            graphView.AddToSelection(node);
            graphView.FrameSelection();
        }



        /// <summary>노드 검색 함수<para>
        /// 노드 제목, 데이터 타입명, GUID와 필드 값으로 검색
        /// </para></summary>
        private void SearchAndFocusNode(string query)
        {
            if (graphView == null)
            {
                return;
            }

            if (!string.Equals(query, previousSearchQuery, StringComparison.OrdinalIgnoreCase))
            {
                previousSearchQuery = query;
                nextSearchIndex = 0;
            }

            IReadOnlyList<GraphNode> matches = graphView.FindNodes(query);
            if (matches.Count == 0) { return; }


            GraphNode node = matches[nextSearchIndex++ % matches.Count];

            graphView.ClearSelection();
            graphView.AddToSelection(node);
            graphView.FrameSelection();
        }

    }
}
