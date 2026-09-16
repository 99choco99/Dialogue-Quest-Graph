namespace UniversalGraph
{
    /// <summary>
    /// Attribute Key가 가리키는 메서드를 누가 가지고 있는지 표시한다
    /// </summary>
    public enum QuestMethodOwner
    {
        /// <summary>현재 IQuestController 객체의 인스턴스 메서드를 호출</summary>
        Controller,

        /// <summary>Controller 인스턴스가 필요 없는 static 메서드를 호출</summary>
        Global
    }
}
