// =====================================================================================
// ItemVariationJson.cs — JSON-формат профиля/результата/реестра (T-VAR01)
// =====================================================================================
// Документация: docs/pickble_items/variation_generator/02_API.md §3
//
// Формат — JsonUtility (Unity), потому что сервер проекта — Unity dedicated server.
// Все поля public + [Serializable]; списки/массивы поддерживаются.
//
// ProfileDocument — то, что кладётся на сервер:
//   {
//     "schemaVersion": 1,
//     "exportedUtc": "...",
//     "exporter": "ProjectC.Items.Variation",
//     "profile": { ... tiers/modifiers/baseStats ... }
//   }
// Внутри profile.baseStats — снапшот статов, поэтому ItemData на сервере не нужен.
// =====================================================================================

using System;
using UnityEngine;

namespace ProjectC.Items.Variation
{
    public static class ItemVariationJson
    {
        public const int CurrentSchemaVersion = 1;
        public const string ExporterId = "ProjectC.Items.Variation";

        [Serializable]
        public class ProfileDocument
        {
            public int schemaVersion = CurrentSchemaVersion;
            public string exportedUtc;
            public string exporter = ExporterId;
            public ItemVariationProfile profile;
        }

        public static string ProfileToJson(ItemVariationProfile profile, bool prettyPrint = true)
        {
            var document = new ProfileDocument
            {
                schemaVersion = CurrentSchemaVersion,
                exportedUtc = DateTime.UtcNow.ToString("o"),
                exporter = ExporterId,
                profile = profile,
            };
            return JsonUtility.ToJson(document, prettyPrint);
        }

        public static bool TryProfileFromJson(
            string json,
            out ItemVariationProfile profile,
            out string error)
        {
            profile = null;
            if (!TryParse(json, out ProfileDocument document, out error)) return false;
            if (document.profile == null)
            {
                error = "JSON не содержит поля 'profile'.";
                return false;
            }
            if (document.schemaVersion > CurrentSchemaVersion)
            {
                error = $"SchemaVersion {document.schemaVersion} новее поддерживаемой {CurrentSchemaVersion}.";
                return false;
            }
            if (document.profile.schemaVersion > ItemVariationProfile.CurrentSchemaVersion)
            {
                error = $"profile.schemaVersion {document.profile.schemaVersion} новее поддерживаемой {ItemVariationProfile.CurrentSchemaVersion}.";
                return false;
            }
            if (document.profile.schemaVersion <= 0)
                document.profile.schemaVersion = ItemVariationProfile.CurrentSchemaVersion;

            profile = document.profile;
            return true;
        }

        public static string ResultToJson(ItemVariationResult result, bool prettyPrint = true)
        {
            return result == null ? string.Empty : JsonUtility.ToJson(result, prettyPrint);
        }

        public static bool TryResultFromJson(string json, out ItemVariationResult result, out string error)
        {
            return TryParse(json, out result, out error);
        }

        public static string ToJson(ItemInstanceRegistrySnapshot snapshot, bool prettyPrint = true)
        {
            return snapshot == null ? string.Empty : JsonUtility.ToJson(snapshot, prettyPrint);
        }

        public static bool TryRegistryFromJson(
            string json,
            out ItemInstanceRegistrySnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (!TryParse(json, out snapshot, out error)) return false;
            if (snapshot.schemaVersion > CurrentSchemaVersion)
            {
                error = $"Реестр schemaVersion {snapshot.schemaVersion} новее поддерживаемой {CurrentSchemaVersion}.";
                return false;
            }
            if (snapshot.schemaVersion <= 0) snapshot.schemaVersion = CurrentSchemaVersion;
            if (snapshot.instances == null) snapshot.instances = Array.Empty<Dto.ItemInstanceDto>();
            return true;
        }

        private static bool TryParse<T>(string json, out T value, out string error) where T : class
        {
            value = null;
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON пуст.";
                return false;
            }
            try
            {
                value = JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                error = "Не удалось разобрать JSON: " + ex.Message;
                return false;
            }
            if (value == null)
            {
                error = "JSON разобран в null (ожидался объект).";
                return false;
            }
            return true;
        }
    }
}
