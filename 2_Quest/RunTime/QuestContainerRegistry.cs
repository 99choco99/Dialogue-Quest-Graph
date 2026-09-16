using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>
    /// Quest 정의 정보를 ID로 찾기 위한 런타임 등록부. 
    /// <para>QuestId로 어느 퀘스트인지 찾기</para>
    /// </summary>
    internal sealed class QuestContainerRegistry
	{
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        private static QuestContainerRegistry instance;

        /// <summary>등록부를 가져올 때 Initialize 호출 누락을 한 곳에서 검사</summary>
        internal static QuestContainerRegistry Instance
        {
            get
            {
                if (instance == null)
                {
                    throw new InvalidOperationException("Quest 등록부를 사용하기 전에 QuestManager.Initialize를 호출하세요.");
                }

                return instance;
            }
            set
            {
                instance = value;
            }
        }

        private readonly Dictionary<int, QuestContainer> containerById = new();
        private readonly Dictionary<int, QuestGraphIndex> indexById = new();

        /// <summary>등록할 퀘스트들의 목록. 전부 다해도 되고 일부만 해서 자원을 아껴도 되고</summary>
        internal IReadOnlyList<QuestContainer> Containers { get; }

        /// <summary>
        /// 전달받은 Quest Container로 조회용 인덱스를 만듭니다.
        /// 등록부 교체는 생성이 끝난 뒤 QuestManager.Initialize에서 처리!!!
        /// </summary>
        internal QuestContainerRegistry(IEnumerable<QuestContainer> containers)
        {
            if (containers == null)
            {
                throw new ArgumentNullException(nameof(containers), "등록할 Quest 정의 목록이 필요합니다.");
            }

            List<QuestContainer> containerList = new();
            foreach (QuestContainer container in containers)
            {
                if (container == null)
                {
                    Debug.LogWarning("[Quest] 등록할 Quest 목록에 null 항목이 있어 무시했습니다.");
                    continue;
                }

                if (container.QuestId <= 0)
                {
                    throw new InvalidOperationException(
                        $"Quest '{container.name}'의 ID {container.QuestId}은 올바르지 않습니다. 양수를 사용하세요.");
                }

                GraphAssetMigrator.Migrate(container);

                if (containerById.TryGetValue(container.QuestId, out QuestContainer duplicateContainer))
                {
                    throw new InvalidOperationException($"중복된 Quest ID {container.QuestId}: '{duplicateContainer.name}', '{container.name}'.");
                }

                QuestGraphIndex index = new (container);

                containerById.Add(container.QuestId, container);
                indexById.Add(container.QuestId, index);

                containerList.Add(container);
            }

            // 외부에서 변하지 않게 읽기 전용으로 제공
            Containers = containerList.AsReadOnly();
        }

        //================================== 조회 함수 ===================================

        /// <summary>Quest container를 찾아 반환</summary>
        internal bool GetContainer(int questId, out QuestContainer container)
        {
            return containerById.TryGetValue(questId, out container);
        }

        /// <summary>등록되어 있는 Quest의 인덱스를 바로 반환</summary>
        internal QuestGraphIndex GetQuestGraphIndex(int questId)
        {
            return indexById[questId];
        }

        /// <summary>그래프의 조회용 인덱스를 반환, container도 겸사겸사</summary>
        internal bool GetQuestGraphIndex(int questId, out QuestContainer container, out QuestGraphIndex index)
        {
            if (GetContainer(questId, out container))
            {
                index = indexById[questId];
                return true;
            }

            index = null;
            return false;
        }
	}
}
