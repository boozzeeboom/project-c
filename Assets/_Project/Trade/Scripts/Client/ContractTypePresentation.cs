using ProjectC.Localization;
using ProjectC.Trade.Config;
using UnityEngine;

namespace ProjectC.Trade.Client
{
    /// <summary>
    /// Resolves contract type presentation from the validated ContractCatalog.
    /// Server-provided DTO metadata takes precedence; the Resources catalog is a
    /// backward-compatible fallback for snapshots produced by older servers.
    /// </summary>
    internal static class ContractTypePresentation
    {
        private static ContractCatalog _fallbackCatalog;
        private static bool _fallbackCatalogResolved;

        public static string GetDisplayName(ContractType type, string localizationKey = null)
        {
            string key = localizationKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                var definition = TryGetDefinition(type);
                key = definition != null ? definition.localizationKey : null;
            }

            return string.IsNullOrWhiteSpace(key)
                ? type.ToString()
                : Loc.Get(key, type.ToString());
        }

        public static string GetUiClass(ContractType type, string uiClass = null)
        {
            if (!string.IsNullOrWhiteSpace(uiClass)) return uiClass;

            var definition = TryGetDefinition(type);
            return definition != null && !string.IsNullOrWhiteSpace(definition.uiClass)
                ? definition.uiClass
                : "type-unknown";
        }

        private static ContractCatalog.ContractTypeDefinition TryGetDefinition(ContractType type)
        {
            if (!_fallbackCatalogResolved)
            {
                _fallbackCatalogResolved = true;
                _fallbackCatalog = Resources.Load<ContractCatalog>("ContractCatalog");
            }

            return _fallbackCatalog != null
                && _fallbackCatalog.TryGetContractType(type, out var definition)
                ? definition
                : null;
        }
    }
}
