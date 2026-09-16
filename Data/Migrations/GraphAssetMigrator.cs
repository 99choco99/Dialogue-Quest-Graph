using System;

namespace UniversalGraph
{
    /// <summary>그래프 에셋 하나를 검사하거나 업그레이드한 결과를 나타내는 클래스</summary>
    public readonly struct GraphAssetMigrationResult
    {
        internal GraphAssetMigrationResult(int fromVersion, int toVersion, bool changed)
        {
            BeforeVersion = fromVersion;
            AfterVersion = toVersion;
            Changed = changed;
        }

        /// <summary>
        /// 변환 전 버전
        /// </summary>
        public int BeforeVersion { get; }

        /// <summary>
        /// 변환 후 버전
        /// </summary>
        public int AfterVersion { get; }

        /// <summary>
        /// 적용 여부
        /// </summary>
        public bool Changed { get; }
    }

    /// <summary>
    /// 그래프 스키마를 정해진 순서로, 여러 번 실행해도 결과가 같도록 업그레이드
    /// 새 버전마다 단계를 추가하고 이미 배포한 단계는 구형 에셋 호환을 위해 수정하지 않는다.
    /// </summary>
    public static class GraphAssetMigrator
    {
        public const int CurrentVersion = 1;

        /// <summary>마이그레이션 시도</summary>
        public static GraphAssetMigrationResult Migrate(GraphContainer container)
        {
            if (container == null)
            {
                throw new InvalidOperationException("그래프 에셋이 필요합니다.");
            }

            int fromVersion = container.SchemaVersion;
            if (fromVersion < 1 || fromVersion > CurrentVersion)
            {
                throw new InvalidOperationException($"그래프 '{container.name}'의 스키마 버전 {fromVersion}은 지원하지 않습니다.");
            }
            if (fromVersion < CurrentVersion)
            {
                throw new InvalidOperationException($"그래프 마이그레이션 {fromVersion} -> {fromVersion + 1}이 정의되어 있지 않습니다.");
            }

            return new GraphAssetMigrationResult(fromVersion, container.SchemaVersion, fromVersion != container.SchemaVersion);
        }

    }
}
