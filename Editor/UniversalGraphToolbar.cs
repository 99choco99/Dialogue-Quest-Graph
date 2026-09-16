using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UniversalGraph.Editor
{
    /// <summary>Toolbar 관리 스크립트</summary>
    internal sealed class UniversalGraphToolbar : Toolbar
    {
        private readonly Label validationStatusLabel;       //오류 몇개 나고 있는지 표시
        private readonly ToolbarButton nextIssueButton;     //오류난 부분 포커스, 이동 해주는 버튼
        private readonly ToolbarSearchField searchField;    //노드 검색을 위한 필드

        private readonly Action<string> searchRequested;    //검색 필드에서 입력된 글자

        /// <summary>도구 모음 UI를 만들고 각 버튼을 그래프 창의 동작과 연결</summary>
        public UniversalGraphToolbar(Action nextIssueRequested, Action<string> searchRequested)
        {
            this.searchRequested = searchRequested;
            AddToClassList(UniversalGraphStyles.ToolbarClass);

            //현재 이슈 찾기 버튼
            nextIssueButton = new ToolbarButton(() => nextIssueRequested?.Invoke()){text = "다음 문제"};
            nextIssueButton.SetEnabled(false);
            Add(nextIssueButton);

            //유효성 검사 결과 표시
            validationStatusLabel = new Label("그래프 없음");
            validationStatusLabel.AddToClassList(UniversalGraphStyles.ValidationStatusClass);
            Add(validationStatusLabel);

            //공간 벌리기
            VisualElement spacer = new();
            spacer.AddToClassList(UniversalGraphStyles.ToolbarSpacerClass);
            Add(spacer);

            //노드 검색필드
            searchField = new ToolbarSearchField();
            searchField.AddToClassList(UniversalGraphStyles.SearchFieldClass);
            searchField.RegisterCallback<KeyDownEvent>(OnSearchKeyDown);            //엔터 키 구독
            Add(searchField);
            Add(new ToolbarButton(RequestSearch) { text = "다음 찾기" });
        }



        /// <summary>
        /// 엔터키 감지
        /// </summary>
        private void OnSearchKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
            {
                return;
            }

            RequestSearch();
            evt.StopPropagation();
        }


        /// <summary>
        /// 검색 요청 이벤트 실행
        /// </summary>
        private void RequestSearch()
        {
            searchRequested?.Invoke(searchField.value?.Trim() ?? string.Empty);
        }


        /// <summary>현재 검증 결과에 맞춰 상태 글자와 관련 버튼을 갱신</summary>
        public void UpdateValidationDisplay(IReadOnlyList<GraphValidationIssue> issues)
        {
            issues ??= Array.Empty<GraphValidationIssue>();
            int errors = issues.Count(issue => issue.Severity == GraphValidationSeverity.Error);
            int warnings = issues.Count - errors;

            validationStatusLabel.text =
                errors == 0 && warnings == 0 ? "문제없음" : $"오류 {errors}개, 경고 {warnings}개";
            validationStatusLabel.tooltip =
                issues.Count == 0 ? "그래프 검증 문제가 없습니다." : string.Join("\n", issues.Select(issue => issue.ToString()));
            UniversalGraphStyles.SetValidationStatus(validationStatusLabel, errors, warnings);

            nextIssueButton.SetEnabled(issues.Any(issue => !string.IsNullOrWhiteSpace(issue.NodeGuid)));
        }
    }
}
