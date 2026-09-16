using System.Collections.Generic;
using UnityEngine;

namespace UniversalGraph
{
    /// <summary>
    /// 직렬화 가능한 단방향 그래프 에셋의 부모 클래스
    /// </summary>
    public abstract class GraphContainer : ScriptableObject
    {
        [SerializeField, HideInInspector]
        private int schemaVersion = GraphAssetMigrator.CurrentVersion;

        /// <summary>순차 마이그레이션에서 사용하는 그래프 데이터 스키마 버전</summary>
        public int SchemaVersion => schemaVersion;

        /// <summary>한 단계의 마이그레이션이 성공한 뒤에 스키마 버전을 올림</summary>
        internal void SetSchemaVersion(int value)
        {
            schemaVersion = value;
        }

        //============================= 실제 데이터 ==========================================

        /// <summary>노드 포트 사이의 직렬화된 단방향 연결 목록</summary>
        public List<NodeLinkData> NodeLinks = new();

        /// <summary>실제 노드 데이터 목록</summary>
        [SerializeReference]
        public List<NodeBaseData> Nodes = new();


    }
}
