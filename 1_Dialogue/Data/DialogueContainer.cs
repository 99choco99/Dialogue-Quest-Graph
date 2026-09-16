using System.Collections.Generic;
using System.Linq;

namespace UniversalGraph
{
    /// <summary>Dialogue 노드와 연결선 데이터를 저장하는 그래프 에셋</summary>
    public class DialogueContainer : GraphContainer
    {
        /// <summary>
        /// entryId로 시작점을 찾기
        /// </summary>
        public bool FindEntryNode(string entryId, out DialogueEntryNodeData entryData, out string error)
        {
            entryData = null;
            if (Nodes == null || Nodes.Count == 0)
            {
                error = $"대화 그래프 '{name}'에 노드가 없습니다.";
                return false;
            }

            //시작점 찾기
            Dictionary<string, DialogueEntryNodeData> entries = new ();
            foreach (DialogueEntryNodeData candidateData in Nodes.OfType<DialogueEntryNodeData>())
            {
                string candidateId = candidateData.EntryId;
                if (!entries.TryAdd(candidateId, candidateData))
                {
                    error = $"대화 그래프 '{name}'에 중복된 진입점 ID '{candidateId}'가 있습니다.";
                    return false;
                }
            }

            string requestedId = string.IsNullOrWhiteSpace(entryId)? DialogueEntryNodeData.DefaultEntryId : entryId.Trim();
            if (!entries.TryGetValue(requestedId, out entryData))
            {
                error = $"대화 그래프 '{name}'에 진입점 '{requestedId}'가 없습니다.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
