using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniversalGraph.Editor
{
    /// <summary>모든 시각 노드를 하나의 타입으로 묶어 일괄 관리하기 위한 최상위 부모 클래스</summary>
    public abstract class GraphNode : Node
	{
		private readonly List<GraphValidationIssue> validationIssues = new();
		private Label validationBadge;

		/// <summary>현재 이 화면 노드에 연결된 직렬화 데이터</summary>
		public abstract NodeBaseData Data { get; }

		/// <summary>이 화면 노드가 지원하는 실제 데이터 타입</summary>
		public abstract Type DataType { get; }

		/// <summary>새 노드를 만들 때 사용하는 초기 크기</summary>
		public virtual Vector2 DefaultSize => new(200f, 150f);

		/// <summary>기존 직렬화 데이터를 연결하고 시각 노드를 구성</summary>
		public abstract void BindNodeData(NodeBaseData data);

		/// <summary>새로 작성하는 노드에 필요한 초기 직렬화 데이터를 생성</summary>
		public abstract NodeBaseData CreateNewData(GraphNodeCreationContext creationContext);

		/// <summary>노드를 선택했을 때 표시할 인스펙터를 생성</summary>
		public virtual VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
		{
			return new VisualElement();
		}

		/// <summary>현재 이 노드에 연결된 작성 단계 진단 결과</summary>
		public IReadOnlyList<GraphValidationIssue> ValidationIssues => validationIssues;

		/// <summary>노드 화면을 다시 만들지 않고 제목 배지와 툴팁만 갱신합니다.</summary>
		internal void SetValidationIssues(IEnumerable<GraphValidationIssue> issues)
		{
			validationIssues.Clear();
			if (issues != null)
			{
				validationIssues.AddRange(issues);
			}

			EnsureValidationBadge();
			if (validationIssues.Count == 0)
			{
				UniversalGraphStyles.SetVisible(validationBadge, false);
				validationBadge.tooltip = string.Empty;
				return;
			}

			int errorCount = validationIssues.Count(issue => issue.Severity == GraphValidationSeverity.Error);
			validationBadge.text = errorCount > 0
				? $"오류 {errorCount}"
				: $"경고 {validationIssues.Count}";
			UniversalGraphStyles.SetValidationBadge(validationBadge, errorCount > 0);
			UniversalGraphStyles.SetVisible(validationBadge, true);
			validationBadge.tooltip = string.Join("\n", validationIssues.Select(issue => issue.ToString()));
		}

		private void EnsureValidationBadge()
		{
			if (validationBadge != null)
			{
				return;
			}

			validationBadge = new Label
			{
				name = "graph-validation-badge"
			};
			validationBadge.AddToClassList(UniversalGraphStyles.ValidationBadgeClass);
			UniversalGraphStyles.SetVisible(validationBadge, false);
			titleContainer.Add(validationBadge);
		}
	}

	//================================================= 제네릭 클래스 ==============================================================



	/// <summary>특정 데이터에 1:1로 바인드 될 시각 노드의 부모 제네릭 클래스</summary>
	public abstract class GraphNode<T> : GraphNode where T : NodeBaseData, new()
	{
		public T NodeData { get; private set; }

		public override NodeBaseData Data => NodeData;

		public override Type DataType => typeof(T);

		//============================노드 데이터 불러올 때=========================

        /// <summary>직렬화 데이터를 한 번 검증하고 연결한 뒤 노드를 구성</summary>
        public override void BindNodeData(NodeBaseData data)
		{
			if (NodeData != null)
			{
				throw new InvalidOperationException($"'{GetType().FullName}' 가 이미 바인드 되어있습니다.");
			}
			if (data is not T typedData)
			{
				throw new ArgumentException(
					$"'{GetType().FullName}' 는 '{typeof(T).FullName}' 가 필요하지만, '{data?.GetType().FullName ?? "null"}'을 받았습니다.", nameof(data));
			}
			ValidateDataForView(typedData);
			NodeData = typedData;
			this.viewDataKey = data.Guid;
			Draw();
		}

		/// <summary>저장된 노드를 그리기 전에 각 노드가 잘못된 데이터인지 검사</summary>
		protected virtual void ValidateDataForView(T data) { }



		//========================= 노드 데이터를 새로 만들 때====================

		/// <summary>
		/// 노드 데이터를 새로 생성할 때 호출하는 함수
		/// </summary>
		public sealed override NodeBaseData CreateNewData(GraphNodeCreationContext creationContext)
		{
			T newData = new();
			InitializeNewData(newData, creationContext);
			newData.Position = creationContext.Position;
			return newData;
		}

		/// <summary>새 노드 데이터를 화면에 연결하기 전에 기본값을 적용</summary>
		protected virtual void InitializeNewData(T data, GraphNodeCreationContext creationContext) { }

        //========================= 공용 ====================

        /// <summary>데이터 연결 후 포트와 시각 요소 그리기</summary>
        protected abstract void Draw();

		/// <summary>노드 선택시 인스펙터에 정보 띄우기</summary>
		public override void OnSelected()
		{
			base.OnSelected();
			GetFirstAncestorOfType<UniversalGraphView>()?.OnNodeSelected(this);
		}

		/// <summary>선택이 해제되면 인스펙터가 제거된 데이터를 계속 참조하지 않도록 알림</summary>
		public override void OnUnselected()
		{
			UniversalGraphView graphView = GetFirstAncestorOfType<UniversalGraphView>();
			base.OnUnselected();
			graphView?.OnNodeUnselected(this);
		}
	}
}


