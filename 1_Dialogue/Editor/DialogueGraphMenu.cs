using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace UniversalGraph.Dialogue.Editor
{
    /// <summary>이름이 있는 기본 시작점 하나를 포함한 Dialogue 그래프 에셋을 만듭니다.</summary>
    internal sealed class DialogueGraphMenu : EndNameEditAction
    {
        [MenuItem("Assets/Create/Universal/Dialogue Graph")]
        private static void CreateDialogueGraph()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0, CreateInstance<DialogueGraphMenu>(), "NewDialogue.asset",
                EditorGUIUtility.FindTexture("ScriptableObject Icon"), null);
        }

        /// <summary>Project 창에서 이름을 확정하면 해당 폴더에 기본 시작점이 있는 그래프를 만듭니다.</summary>
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            DialogueContainer container = CreateInstance<DialogueContainer>();
            GraphAssetMigrator.Migrate(container);

            container.Nodes.Add(new DialogueEntryNodeData
            {
                Position = new Vector2(100f, 100f),
                EntryId = DialogueEntryNodeData.DefaultEntryId
            });

            AssetDatabase.CreateAsset(container, pathName);
            ProjectWindowUtil.ShowCreatedAsset(container);
        }
    }
}
