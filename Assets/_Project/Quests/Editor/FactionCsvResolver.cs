// Project C: CSV faction identity resolver.
// New CSV data uses factionKey; preserved enum names remain supported as legacy aliases.

#if UNITY_EDITOR
using System;
using UnityEngine;
using ProjectC.Factions;
using ProjectC.Knowledge;

namespace ProjectC.Quests.Editor
{
    public static class FactionCsvResolver
    {
        public static bool TryResolve(string raw, out FactionDefinition definition, out string error)
        {
            definition = null;
            error = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string value = raw.Trim();
            var catalog = FactionCatalog.Instance ?? new FactionCatalog();
            if (catalog.TryGetByFactionKey(value, out definition))
                return true;

            if (Enum.TryParse<FactionId>(value, true, out var legacyId) &&
                catalog.TryGet(legacyId, out definition))
                return true;

            error = $"Unknown faction '{value}'. Expected factionKey or a legacy FactionId name.";
            return false;
        }
    }
}
#endif
