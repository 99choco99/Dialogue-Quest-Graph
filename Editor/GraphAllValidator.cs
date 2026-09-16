using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UniversalGraph.Editor
{
    /// <summary>스크립트 재로드 후와 빌드 전에 메서드, 전체 그래프를 검사하고, Error가 있으면 빌드를 중단</summary>
    internal sealed class GraphAllValidator : AssetPostprocessor, IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        /// <summary>Unity가 스크립트 재로드와 에셋 임포트를 마친 뒤 호출</summary>
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload)
            {
                ValidateAll();
            }
        }

        /// <summary>빌드 전에 다시 검사. Warning은 허용하고 Error만 빌드를 중단</summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            int errorCount = ValidateAll();
            if (errorCount > 0)
            {
                throw new BuildFailedException($"Universal Graph 오류 {errorCount}개로 빌드를 중단했습니다. Console을 확인하세요.");
            }
        }

        /// <summary>에셋을 수정하지 않고 모든 진단을 Console에 출력한 뒤 Error 개수를 반환</summary>
        private static int ValidateAll()
        {
            List<string> errors = GraphMethodValidator.Validate();
            foreach (string error in errors)
            {
                Debug.LogError("[Universal Graph] " + error);
            }

            int errorCount = errors.Count;
            foreach (string assetPath in AssetDatabase.FindAssets("t:GraphContainer")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                // 한 파일에 함께 저장된 그래프도 빠뜨리지 않습니다.
                foreach (GraphContainer container in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<GraphContainer>())
                {
                    foreach (GraphValidationIssue issue in GraphValidator.Validate(container))
                    {
                        string message = $"[Universal Graph] '{assetPath}' ({container.name}) {issue}";
                        if (issue.Severity == GraphValidationSeverity.Error)
                        {
                            errorCount++;
                            Debug.LogError(message, container);
                        }
                        else
                        {
                            Debug.LogWarning(message, container);
                        }
                    }
                }
            }
            return errorCount;
        }
    }
}
