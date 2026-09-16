using UnityEditor.Experimental.GraphView;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>여러 분기로 이어질 수 있는 Quest 노드의 공통 포트 구조를 설계하기 위한 클래스</summary>
    public abstract class QuestFlowNode<T> : GraphNode<T> where T : NodeBaseData, new()
    {
        protected abstract string NodeTitle { get; }
        protected virtual bool HasOutput => true;

        /// <summary>공통 다중 입력 포트와 선택적인 다중 출력 포트를 만듭니다.</summary>
        protected override void Draw()
        {
            RefreshTitle();

            Port input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(float));
            input.portName = QuestPortNames.Input;
            inputContainer.Add(input);

            if (HasOutput)
            {
                Port next = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
                next.portName = QuestPortNames.Next;
                outputContainer.Add(next);
            }

            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>현재 표시되는 노드 제목을 다시 만들기</summary>
        protected void RefreshTitle()
        {
            title = NodeTitle;
        }
    }
}
