using System;
using System.Collections.Generic;

namespace UniversalGraph.Editor
{
    /// <summary>검증기가 검증할 대상의 타입과 검증을 정의하는 인터페이스</summary>
    public interface IGraphValidator
    {
        Type ContainerType { get; }
        void Validate(GraphValidationIndex index, ICollection<GraphValidationIssue> issues);
    }

    /// <summary>그래프 검증기 부모 타입</summary>
    public abstract class GraphValidatorBase<TContainer> : IGraphValidator where TContainer : GraphContainer
    {
        public Type ContainerType => typeof(TContainer);

        /// <summary>컨테이너 타입을 확인하고 검증 함수에게 인계</summary>
        public void Validate(GraphValidationIndex index, ICollection<GraphValidationIssue> issues)
        {
            Validate((TContainer)index.Container, index, issues);
        }

        /// <summary>실제 그래프 컨테이너에 필요한 검증 규칙을 구현</summary>
        protected abstract void Validate(TContainer container, GraphValidationIndex index, ICollection<GraphValidationIssue> issues);
    }
}
