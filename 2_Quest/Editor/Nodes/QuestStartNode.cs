using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UniversalGraph.Editor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>Quest 진행을 시작하는 시작 노드</summary>
    [GraphNodeEditor(typeof(QuestContainer), "Quest/Start/Quest Start")]
    public sealed class QuestStartNode : GraphNode<QuestStartNodeData>
    {
        public override Vector2 DefaultSize => new(170f, 90f);

        /// <summary>같은 그래프에 두 번째 Quest Start가 생성되는 것을 막습니다.</summary>
        protected override void InitializeNewData(QuestStartNodeData data, GraphNodeCreationContext creationContext)
        {
            if (creationContext.ExistingNodes.Any(node => node is QuestStartNodeData))
            {
                throw new System.InvalidOperationException("Quest 그래프에는 Quest Start 노드를 하나만 만들 수 있습니다.");
            }
        }

        /// <summary>시작 노드의 UI 그리기</summary>
        protected override void Draw()
        {
            capabilities &= ~Capabilities.Copiable;
            Port next = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(float));
            next.portName = QuestPortNames.Next;
            outputContainer.Add(next);

            title = "QUEST START";
            AddToClassList("start-node");
            RefreshPorts();
            RefreshExpandedState();
        }

        /// <summary>퀘스트 시작노드 인스펙터 그리기</summary>
        public override VisualElement CreateInspector(NodeInspectorEditHandler editHandler)
        {
            return new HelpBox("Quest 진행은 여기에서 시작합니다. 상호작용 진입점은 별도이며 Quest를 시작하지 않습니다.", HelpBoxMessageType.Info);
        }
    }
}
