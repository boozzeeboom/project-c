// =====================================================================================
// ProgressInfo.cs — DTO прогресса выполнения требования (Project C: The Clouds)
// =====================================================================================
// Документация:
//   • docs/MetaRequirement/00_OVERVIEW.md §2.7
//
// Назначение: проекция состояния "сколько собрано из скольки нужно" для UI tooltip'а
// и для `MetaRequirement.GetPlayerProgress(clientId)`. Чистая структура, без Unity-зависимостей
// (легко покрыть unit-тестом, если будет нужно).
// =====================================================================================

namespace ProjectC.MetaRequirement
{
    /// <summary>
    /// Прогресс-инфо: сколько предметов из списка уже есть у игрока, каких не хватает,
    /// выполнено ли требование.
    /// </summary>
    public struct ProgressInfo
    {
        /// <summary>Сколько РАЗНЫХ уникальных предметов нужно для выполнения
        /// (для <see cref="RequirementLogic.All"/> = <c>_requiredItems.Length</c>;
        /// для <see cref="RequirementLogic.AtLeastN"/> = <c>_requiredCount</c>;
        /// для <see cref="RequirementLogic.Any"/> = 1).</summary>
        public int Required;

        /// <summary>Сколько РАЗНЫХ уникальных предметов из списка у игрока ЕСТЬ.</summary>
        public int Have;

        /// <summary>itemId недостающих предметов (только уникальные).
        /// Для <see cref="RequirementLogic.Any"/> = все itemId из списка (раз требуется любой,
        /// то "не хватает" — это все, которых нет).</summary>
        public int[] MissingIds;

        /// <summary>Выполнено ли требование для данного игрока.</summary>
        public bool Satisfied;

        public override string ToString() => $"Прогресс: {Have}/{Required}";
    }
}
