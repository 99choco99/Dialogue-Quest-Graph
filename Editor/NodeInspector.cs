using System;
using UnityEngine.UIElements;

namespace UniversalGraph.Editor
{
	/// <summary>선택한 노드의 정보를 띄워주는 인스펙터에 관한 클래스</summary>
	public sealed class NodeInspector : ScrollView
	{
		private readonly NodeInspectorEditHandler editHandler;

		private readonly VisualElement validationRoot = new();	//유효성 검사결과가 들어갈 부분
		private readonly VisualElement contentRoot = new();		//실제 내용이 들어갈 부분
		private GraphNode selectedGraphNode;

		public NodeInspector(Action<string, Action> applyDataEdit, Action<string, Action> applyStructureEdit)
		{
			editHandler = new NodeInspectorEditHandler(applyDataEdit, applyStructureEdit);
			Add(validationRoot);
			Add(contentRoot);
		}

		/// <summary>노드 선택시 인스펙터의 내용을 해당 노드로 업데이트</summary>
		public void UpdateInspector(GraphNode selectedNode)
		{
			selectedGraphNode = selectedNode;
            contentRoot.Clear();
            if (selectedGraphNode != null)
			{
				VisualElement inspectorContent = selectedGraphNode.CreateInspector(editHandler);
				if (inspectorContent != null)
				{
					contentRoot.Add(inspectorContent);
				}
			}

			RefreshValidationDisplay();

		}

		/// <summary>필드 편집 도중 인스펙터 전체를 업데이트 하지 않고 진단 표시만 갱신</summary>
		public void RefreshValidationDisplay()
		{
			validationRoot.Clear();
			if (selectedGraphNode == null)
			{
				return;
			}

			foreach (GraphValidationIssue issue in selectedGraphNode.ValidationIssues)
			{
				validationRoot.Add(
					new HelpBox($"[{issue.IssueKind}] {issue.Message}",
					issue.Severity == GraphValidationSeverity.Error ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning));
			}
		}
	}
}
