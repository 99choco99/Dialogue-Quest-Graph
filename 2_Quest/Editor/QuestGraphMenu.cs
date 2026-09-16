using System.Linq;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace UniversalGraph.Quest.Editor
{
    /// <summary>고유 ID와 필수 진행 시작점을 가진 Quest 그래프를 만듭니다.</summary>
    internal sealed class QuestGraphMenu : EndNameEditAction
    {
        [MenuItem("Assets/Create/Universal/Quest Graph")]
        private static void CreateQuestGraph()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0, CreateInstance<QuestGraphMenu>(), "NewQuest.asset",
                EditorGUIUtility.FindTexture("ScriptableObject Icon"), null);
        }

        /// <summary>확정한 파일명으로 Quest 이름을 정하고 고유 ID와 시작점을 함께 저장합니다.</summary>
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            int nextQuestId = AssetDatabase.FindAssets("t:QuestContainer")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<QuestContainer>)
                .Where(questContainer => questContainer != null && questContainer.QuestId > 0)
                .Select(questContainer => questContainer.QuestId)
                .DefaultIfEmpty(0)
                .Max() + 1;
            var container = ScriptableObject.CreateInstance<QuestContainer>();
            GraphAssetMigrator.Migrate(container);
            container.QuestId = nextQuestId;
            container.questName = System.IO.Path.GetFileNameWithoutExtension(pathName);
            container.Nodes.Add(new QuestStartNodeData
            {
                Position = new Vector2(100f, 100f)
            });

            AssetDatabase.CreateAsset(container, pathName);
            ProjectWindowUtil.ShowCreatedAsset(container);
        }
    }
}
