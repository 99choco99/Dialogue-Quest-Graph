using System;

namespace UniversalGraph.Editor
{
	/// <summary>노드 인스펙터에서의 수정을 Undo와 저장 처리 함수로 전달</summary>
	public sealed class NodeInspectorEditHandler
	{
		private readonly Action<string, Action> applyDataEdit;
		private readonly Action<string, Action> applyStructureEdit;

		public NodeInspectorEditHandler(Action<string, Action> applyDataEdit, Action<string, Action> applyStructureEdit)
		{
			this.applyDataEdit = applyDataEdit
				?? throw new ArgumentNullException(nameof(applyDataEdit), "인스펙터 데이터 수정 함수가 필요합니다.");
			this.applyStructureEdit = applyStructureEdit
				?? throw new ArgumentNullException(nameof(applyStructureEdit), "인스펙터 구조 수정 함수가 필요합니다.");
		}

		/// <summary>필드의 데이터 수정을 기록</summary>
		public void ApplyDataEdit(string undoName, Action edit)
		{
			if (edit == null)
			{
				throw new ArgumentNullException(nameof(edit), "적용할 데이터 수정 작업이 필요합니다.");
			}
			applyDataEdit.Invoke(undoName, edit);
		}

		/// <summary>포트나 연결까지 바꾸는 구조 수정을 기록</summary>
		public void ApplyStructureEdit(string undoName, Action edit)
		{
			if (edit == null)
			{
				throw new ArgumentNullException(nameof(edit), "적용할 구조 수정 작업이 필요합니다.");
			}
			applyStructureEdit.Invoke(undoName, edit);
		}
	}
}
