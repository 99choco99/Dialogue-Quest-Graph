using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.UnityLinker;

namespace UniversalGraph.Editor
{
    /// <summary>UPM 패키지 안의 link.xml도 빌드의 코드 보존 설정에 포함합니다.</summary>
    internal sealed class GraphLinkerProcessor : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        /// <summary>Unity가 링커 실행 전에 호출하며, 기존 link.xml의 실제 경로를 전달합니다.</summary>
        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            string path = AssetDatabase.GUIDToAssetPath("274806e639694e4b84f4d35e15d27c36");
            PackageInfo package = PackageInfo.FindForAssetPath(path);
            if (package != null)
            {
                path = Path.Combine(package.resolvedPath, "link.xml");
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                throw new BuildFailedException("Universal Graph의 link.xml을 찾을 수 없습니다. 패키지 설치를 확인하세요.");
            }

            return Path.GetFullPath(path);
        }
    }
}
