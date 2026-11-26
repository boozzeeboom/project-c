// =====================================================================================
// ItemVariationProfileAsset.cs — ScriptableObject-профиль вариации (T-VAR01)
// =====================================================================================
// Документация: docs/pickble_items/variation_generator/03_EDITOR_TOOL.md
//
// Ассет — авторинг-обёртка над ItemVariationProfile:
//   • baseItem = ItemData, с которого снят снапшот статов;
//   • profile  = сами правила (тиры/аффиксы/базовые статы);
//   • notes    = заметки дизайнера.
//
// Runtime/сервер ассет не читает: он получает JSON (ItemVariationJson.ProfileDocument).
// Это позволяет правозить профиль в редакторе, а на сервер отправлять только JSON.
// =====================================================================================

using UnityEngine;

namespace ProjectC.Items.Variation
{
    [CreateAssetMenu(fileName = "ItemVariation_", menuName = "Project C/Items/Variation Profile", order = 3)]
    public class ItemVariationProfileAsset : ScriptableObject
    {
        [Tooltip("Базовый pickable-предмет (ItemData), для которого крутятся статы.")]
        public ItemData baseItem;

        [Tooltip("Правила вариации: тиры, аффиксы, базовые статы-снапшот.")]
        public ItemVariationProfile profile = new ItemVariationProfile();

        [TextArea(2, 6)]
        public string notes;

        /// <summary>Снять базовые статы и идентичность с baseItem (id не меняется, если не задан).</summary>
        public void CaptureFromItem(int baseItemId = -1)
        {
            if (profile == null) profile = new ItemVariationProfile();
            ItemVariationProfileFactory.CaptureInto(profile, baseItem, baseItemId);
        }

        public bool TryRoll(long seed, out ItemVariationResult result, out string error, int luckBps = 0)
        {
            if (profile == null)
            {
                result = null;
                error = "У ассета нет профиля.";
                return false;
            }
            return ItemVariationGenerator.TryRoll(profile, seed, out result, out error, luckBps);
        }

        /// <summary>JSON для сервера (снапшот профиля + базовые статы).</summary>
        public string ToServerJson(bool prettyPrint = true)
        {
            return ItemVariationJson.ProfileToJson(profile, prettyPrint);
        }

        private void OnValidate()
        {
            if (profile == null)
                profile = new ItemVariationProfile();
            if (profile.schemaVersion <= 0)
                profile.schemaVersion = ItemVariationProfile.CurrentSchemaVersion;
        }
    }
}
