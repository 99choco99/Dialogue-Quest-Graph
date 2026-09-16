using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniversalGraph.Editor
{
    /// <summary>
    /// USS를 불러오고 상태에 따라 스타일 클래스만 교체
    /// 크기, 색상, 여백 같은 디자인 값은 UniversalGraph.uss에서 관리
    /// </summary>
    public static class UniversalGraphStyles
    {
        public const string ToolbarClass = "universal-graph-toolbar";
        public const string ValidationStatusClass = "universal-graph-validation-status";
        public const string ToolbarSpacerClass = "universal-graph-toolbar-spacer";
        public const string SearchFieldClass = "universal-graph-search-field";
        public const string SplitViewClass = "universal-graph-split-view";
        public const string ValidationBadgeClass = "universal-graph-validation-badge";

        private const string StyleSheetGuid = "2e824f0a10f2485cbba5a3cc5f2cda91";
        private const string HiddenClass = "universal-graph-hidden";
        private const string ErrorClass = "universal-graph-error";
        private const string WarningClass = "universal-graph-warning";
        private const string ValidClass = "universal-graph-valid";

        private static StyleSheet styleSheet;
        private static bool didReportMissingStyleSheet;

        private static StyleSheet LoadStyleSheet()
        {
            string path = AssetDatabase.GUIDToAssetPath(StyleSheetGuid);
            StyleSheet loaded = string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (loaded == null && !didReportMissingStyleSheet)
            {
                didReportMissingStyleSheet = true;
                Debug.LogError("[Universal Graph] 공통 스타일시트 UniversalGraph.uss를 찾지 못했습니다.");
            }

            return loaded;
        }



        /// <summary>그래프 창 루트에 공통 USS를 중복 없이 연결</summary>
        public static void AttachTo(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            styleSheet = styleSheet != null ? styleSheet : LoadStyleSheet();
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }
        }

        /// <summary> VisualElement를 보여줄지 말지 </summary>
        public static void SetVisible(VisualElement element, bool isVisible)
        {
            element?.EnableInClassList(HiddenClass, !isVisible);
        }

        /// <summary>검증 상태 글자의 오류, 경고, 정상 색상 설정</summary>
        public static void SetValidationStatus(VisualElement element, int errorCount, int warningCount)
        {
            if (element == null)
            {
                return;
            }

            element.EnableInClassList(ErrorClass, errorCount > 0);
            element.EnableInClassList(WarningClass, errorCount == 0 && warningCount > 0);
            element.EnableInClassList(ValidClass, errorCount == 0 && warningCount == 0);
        }


        /// <summary>오류난 노드의 색상을 변경</summary>
        public static void SetValidationBadge(VisualElement element, bool hasErrors)
        {
            if (element == null)
            {
                return;
            }

            element.EnableInClassList(ErrorClass, hasErrors);
            element.EnableInClassList(WarningClass, !hasErrors);
        }


    }
}
