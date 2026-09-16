using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>프로젝트에 있는 Quest 에셋 목록을 보관</summary>
    internal static class QuestAssetCatalog
    {
        private static IReadOnlyList<QuestContainer> containers;

        static QuestAssetCatalog()
        {
            EditorApplication.projectChanged += () => containers = null;
        }

        public static IReadOnlyList<QuestContainer> Containers
        {
            get
            {
                if (containers == null || containers.Any(container => container == null))
                {
                    containers = AssetDatabase.FindAssets("t:QuestContainer")
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Select(AssetDatabase.LoadAssetAtPath<QuestContainer>)
                        .Where(container => container != null)
                        .OrderBy(container => container.QuestId)
                        .ThenBy(container => container.name, StringComparer.Ordinal)
                        .ToArray();
                }

                return containers;
            }
        }

    }
}
