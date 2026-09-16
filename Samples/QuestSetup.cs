using System.Collections.Generic;
using UnityEngine;

namespace UniversalGraph.Samples
{
    /// <summary>Quest 목록 등록과 플레이어 진행 기록을 한곳에 연결하는 최소 사용 예제.</summary>

    [AddComponentMenu("Universal/Samples/Quest Setup")]
    // 기본 실행 순서(0)의 컴포넌트가 Quest API를 사용하기 전에 목록을 등록합니다.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class QuestSetup : MonoBehaviour, IQuestController
    {
        [Tooltip("게임에서 사용할 Quest 그래프 에셋 목록입니다. 등록만 하며 자동으로 시작하지 않습니다.")]
        [SerializeField] private List<QuestContainer> questContainers = new();

        /// <summary>이 플레이어의 실행 중 진행 기록. 그래프 에셋과는 별개이며 파일 저장은 자동으로 하지 않습니다.</summary>
        public IDictionary<int, QuestProgress> QuestProgress { get; } = new Dictionary<int, QuestProgress>();

        //============================== 등록 ==============================

        /// <summary>Inspector에서 지정한 목록을 게임 시작 시 등록합니다. 다시 호출하면 등록 목록 전체가 교체됩니다.</summary>
        private void Awake()
        {
            QuestManager.Initialize(questContainers);
        }

        //============================ 게임 연결 ============================

        /// <summary>Manager가 보내는 진행 변경 알림. 실제 게임에서는 이곳을 퀘스트 UI 갱신 등에 연결합니다.</summary>
        public void OnQuestProgressChanged(QuestContainer container, QuestProgress progress)
        {
            // 샘플에서는 연결을 확인할 수 있도록 현재 상태만 출력합니다.
            Debug.Log($"[Quest] {container.questName} (ID: {container.QuestId}) - {progress.state}", this);
        }
    }
}
