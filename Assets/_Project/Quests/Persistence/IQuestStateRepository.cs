// T-Q18: repository interface для quest state persistence.
// Per 09_OPEN_QUESTIONS.md §H: fire-and-forget immediate save on every state change.

using System;

namespace ProjectC.Quests.Persistence
{
    public interface IQuestStateRepository
    {
        /// <summary>Load player state (or null если no save exists).</summary>
        QuestSaveData Load(ulong clientId);

        /// <summary>Save player state atomic. Returns true если ok.</summary>
        bool Save(ulong clientId, QuestSaveData data);

        /// <summary>Optional: wipe player save (для admin/dev tools).</summary>
        bool Delete(ulong clientId);

        /// <summary>Подсчёт игроков с сохранённым state (для telemetry/admin UI).</summary>
        int SavedPlayerCount { get; }
    }
}
