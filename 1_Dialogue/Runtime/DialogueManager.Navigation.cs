namespace UniversalGraph
{
    /// <summary>Dialogue 노드 연결 조회와 현재 노드 이동을 담당</summary>
    public sealed partial class DialogueManager
    {
        /// <summary>
        /// 다음 노드로 넘어가기, 성공여부 반환
        /// </summary>
        private bool MoveToNextNode(string nodeGuid, string portName)
        {
            if (!GetNextNode(nodeGuid, portName, out NodeBaseData nextNodeData, out string error))
            {
                FailConversation($"[Dialogue] {error}");
                return false;
            }

            currentNodeData = nextNodeData;
            return true;
        }

        /// <summary>output 포트 정보를 통해 다음 노드가 뭔지 알아내기</summary>
        private bool GetNextNode(string nodeGuid, string portName, out NodeBaseData nextNodeData, out string error)
        {
            nextNodeData = null;

            //링크정보 가져오기
            if (!linkDataByOutput.TryGetValue((nodeGuid, portName), out NodeLinkData linkData))
            {
                error = $"노드 '{nodeGuid}'의 출력 포트 '{portName}'에 연결선이 없습니다.";
                return false;
            }

            //그걸로 노드 guid가져오기
            string targetGuid = linkData.TargetNodeGuid;
            if (!nodeDataByGuid.TryGetValue(targetGuid, out nextNodeData))
            {
                error = $"연결선이 가리키는 대상 노드 '{targetGuid}'가 존재하지 않습니다.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 현재 대기 상태를 정리하고 지정한 출력 포트의 다음 노드부터 실행을 재개
        /// </summary>
        private void ProceedToNextNode(string portName)
        {
            if (!IsConversationActive)
            {
                return;
            }

            ResetBlockingState();

            if (MoveToNextNode(currentNodeData.Guid, portName))
            {
                RunUntilBlocked();
            }
        }
    }
}
