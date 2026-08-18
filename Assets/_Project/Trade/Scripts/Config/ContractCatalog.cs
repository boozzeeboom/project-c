using System;
using System.Collections.Generic;
using ProjectC.Trade;
using UnityEngine;

namespace ProjectC.Trade.Config
{
    /// <summary>
    /// Immutable-at-runtime catalog for contract locations, route distances and
    /// publishable contract type definitions.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ContractCatalog",
        menuName = "ProjectC/Trade/Contract Catalog")]
    public sealed class ContractCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class LocationDefinition
        {
            [Tooltip("Canonical location ID. Runtime lookups use uppercase trimmed values.")]
            public string locationId = "";

            [Tooltip("Location is available for contract generation.")]
            public bool enabled = true;
        }

        [Serializable]
        public sealed class DistanceDefinition
        {
            public string fromLocationId = "";
            public string toLocationId = "";

            [Min(0f)]
            public float distanceKm;
        }

        [Serializable]
        public sealed class ContractTypeDefinition
        {
            public ContractType type = ContractType.Standard;

            [Tooltip("Only publishable definitions are generated on the offer board.")]
            public bool publishable = true;

            [Tooltip("Reward multiplier applied after the common delivery formula.")]
            [Min(0f)]
            public float rewardMultiplier = 1f;

            [Tooltip("Time limit for this contract type in seconds. 0 = no limit.")]
            [Min(0f)]
            public float timeLimitSeconds = 300f;

            [Tooltip("Receipt semantics are not enabled unless the full acceptance/settlement flow exists.")]
            public bool isReceiptContract;

            [Tooltip("Localization key for the contract type badge in client UI.")]
            public string localizationKey = "";

            [Tooltip("USS class for the contract type badge in client UI.")]
            public string uiClass = "";

            [Tooltip("Fallback text used in server-side operation messages.")]
            public string displayNameFallback = "";

            [ColorUsage(false, true)]
            public Color uiColor = Color.white;
        }

        [Header("Locations")]
        public List<LocationDefinition> locations = new List<LocationDefinition>();

        [Header("Route distances")]
        public List<DistanceDefinition> distances = new List<DistanceDefinition>();

        [Header("Contract types")]
        public List<ContractTypeDefinition> contractTypes = new List<ContractTypeDefinition>();

        public bool HasLocation(string locationId)
        {
            string key = MarketConfigCollector.NormalizeLocationId(locationId);
            if (string.IsNullOrEmpty(key) || locations == null) return false;

            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location != null
                    && location.enabled
                    && MarketConfigCollector.NormalizeLocationId(location.locationId) == key)
                {
                    return true;
                }
            }

            return false;
        }

        public List<string> GetEnabledLocationIds()
        {
            var result = new List<string>();
            if (locations == null) return result;

            var seen = new HashSet<string>();
            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location == null || !location.enabled) continue;

                string key = MarketConfigCollector.NormalizeLocationId(location.locationId);
                if (!string.IsNullOrEmpty(key) && seen.Add(key))
                    result.Add(key);
            }

            return result;
        }

        public bool TryGetDistance(string fromLocationId, string toLocationId, out float distanceKm)
        {
            string fromKey = MarketConfigCollector.NormalizeLocationId(fromLocationId);
            string toKey = MarketConfigCollector.NormalizeLocationId(toLocationId);
            distanceKm = 0f;

            if (string.IsNullOrEmpty(fromKey) || string.IsNullOrEmpty(toKey) || distances == null)
                return false;

            for (int i = 0; i < distances.Count; i++)
            {
                var distance = distances[i];
                if (distance == null) continue;

                string entryFrom = MarketConfigCollector.NormalizeLocationId(distance.fromLocationId);
                string entryTo = MarketConfigCollector.NormalizeLocationId(distance.toLocationId);
                if ((entryFrom == fromKey && entryTo == toKey)
                    || (entryFrom == toKey && entryTo == fromKey))
                {
                    distanceKm = Mathf.Max(0f, distance.distanceKm);
                    return true;
                }
            }

            return false;
        }

        public List<ContractTypeDefinition> GetPublishableContractTypes()
        {
            var result = new List<ContractTypeDefinition>();
            if (contractTypes == null) return result;

            var seen = new HashSet<ContractType>();
            for (int i = 0; i < contractTypes.Count; i++)
            {
                var definition = contractTypes[i];
                if (definition == null || !definition.publishable || !seen.Add(definition.type)) continue;
                result.Add(definition);
            }

            return result;
        }

        public bool TryGetContractType(ContractType type, out ContractTypeDefinition definition)
        {
            definition = null;
            if (contractTypes == null) return false;

            for (int i = 0; i < contractTypes.Count; i++)
            {
                var candidate = contractTypes[i];
                if (candidate != null && candidate.type == type)
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool Validate(out List<string> errors)
        {
            errors = new List<string>();
            var locationIds = new HashSet<string>();
            int enabledLocationCount = 0;

            if (locations == null || locations.Count == 0)
            {
                errors.Add("locations is empty");
            }
            else
            {
                for (int i = 0; i < locations.Count; i++)
                {
                    var location = locations[i];
                    if (location == null)
                    {
                        errors.Add($"locations[{i}] is null");
                        continue;
                    }

                    string key = MarketConfigCollector.NormalizeLocationId(location.locationId);
                    if (string.IsNullOrEmpty(key))
                    {
                        errors.Add($"locations[{i}] has an empty locationId");
                        continue;
                    }

                    if (!locationIds.Add(key))
                        errors.Add($"duplicate locationId '{key}'");
                    if (location.enabled) enabledLocationCount++;
                }
            }

            if (enabledLocationCount < 2)
                errors.Add("at least two enabled locations are required");

            if (distances != null)
            {
                for (int i = 0; i < distances.Count; i++)
                {
                    var distance = distances[i];
                    if (distance == null)
                    {
                        errors.Add($"distances[{i}] is null");
                        continue;
                    }

                    string from = MarketConfigCollector.NormalizeLocationId(distance.fromLocationId);
                    string to = MarketConfigCollector.NormalizeLocationId(distance.toLocationId);
                    if (!locationIds.Contains(from) || !locationIds.Contains(to))
                        errors.Add($"distances[{i}] references an unknown location");
                    if (from == to)
                        errors.Add($"distances[{i}] references the same location twice");
                    if (distance.distanceKm <= 0f)
                        errors.Add($"distances[{i}] must have distanceKm > 0");
                }
            }

            var enabledLocationIds = GetEnabledLocationIds();
            for (int i = 0; i < enabledLocationIds.Count; i++)
            {
                for (int j = i + 1; j < enabledLocationIds.Count; j++)
                {
                    if (!TryGetDistance(enabledLocationIds[i], enabledLocationIds[j], out _))
                    {
                        errors.Add($"missing distance for '{enabledLocationIds[i]}' ↔ '{enabledLocationIds[j]}'");
                    }
                }
            }

            var types = new HashSet<ContractType>();
            bool hasPublishableType = false;
            if (contractTypes == null || contractTypes.Count == 0)
            {
                errors.Add("contractTypes is empty");
            }
            else
            {
                for (int i = 0; i < contractTypes.Count; i++)
                {
                    var definition = contractTypes[i];
                    if (definition == null)
                    {
                        errors.Add($"contractTypes[{i}] is null");
                        continue;
                    }

                    if (!types.Add(definition.type))
                        errors.Add($"duplicate contract type '{definition.type}'");
                    if (definition.publishable) hasPublishableType = true;
                    if (definition.publishable && definition.isReceiptContract)
                        errors.Add($"contract type '{definition.type}' cannot be publishable before Receipt flow is implemented");
                    if (string.IsNullOrWhiteSpace(definition.localizationKey))
                        errors.Add($"contractTypes[{i}] localizationKey is empty");
                    if (string.IsNullOrWhiteSpace(definition.uiClass))
                        errors.Add($"contractTypes[{i}] uiClass is empty");
                    if (string.IsNullOrWhiteSpace(definition.displayNameFallback))
                        errors.Add($"contractTypes[{i}] displayNameFallback is empty");
                    if (definition.rewardMultiplier < 0f)
                        errors.Add($"contractTypes[{i}] rewardMultiplier must be >= 0");
                    if (definition.timeLimitSeconds < 0f)
                        errors.Add($"contractTypes[{i}] timeLimitSeconds must be >= 0");
                }
            }

            if (!hasPublishableType)
                errors.Add("no publishable contract type is configured");

            return errors.Count == 0;
        }

        /// <summary>
        /// Runtime fallback preserving the current four locations, distances and
        /// Standard/Urgent/Receipt behavior when no catalog asset is present.
        /// </summary>
        public static ContractCatalog CreateDefaultRuntime()
        {
            var catalog = CreateInstance<ContractCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;

            catalog.locations = new List<LocationDefinition>
            {
                new LocationDefinition { locationId = "PRIMIUM" },
                new LocationDefinition { locationId = "SECUNDUS" },
                new LocationDefinition { locationId = "TERTIUS" },
                new LocationDefinition { locationId = "QUARTUS" }
            };

            catalog.distances = new List<DistanceDefinition>
            {
                new DistanceDefinition { fromLocationId = "PRIMIUM", toLocationId = "SECUNDUS", distanceKm = 120f },
                new DistanceDefinition { fromLocationId = "PRIMIUM", toLocationId = "TERTIUS", distanceKm = 200f },
                new DistanceDefinition { fromLocationId = "PRIMIUM", toLocationId = "QUARTUS", distanceKm = 180f },
                new DistanceDefinition { fromLocationId = "SECUNDUS", toLocationId = "TERTIUS", distanceKm = 150f },
                new DistanceDefinition { fromLocationId = "SECUNDUS", toLocationId = "QUARTUS", distanceKm = 160f },
                new DistanceDefinition { fromLocationId = "TERTIUS", toLocationId = "QUARTUS", distanceKm = 100f }
            };

            catalog.contractTypes = new List<ContractTypeDefinition>
            {
                new ContractTypeDefinition
                {
                    type = ContractType.Standard,
                    publishable = true,
                    rewardMultiplier = 1f,
                    timeLimitSeconds = 300f,
                    isReceiptContract = false,
                    localizationKey = "ui.contract.type.standard",
                    uiClass = "type-standard",
                    displayNameFallback = "[Стандарт]",
                    uiColor = new Color(0.3f, 0.6f, 1f)
                },
                new ContractTypeDefinition
                {
                    type = ContractType.Urgent,
                    publishable = true,
                    rewardMultiplier = 1.5f,
                    timeLimitSeconds = 150f,
                    isReceiptContract = false,
                    localizationKey = "ui.contract.type.urgent",
                    uiClass = "type-urgent",
                    displayNameFallback = "[Срочный]",
                    uiColor = new Color(1f, 0.5f, 0f)
                },
                new ContractTypeDefinition
                {
                    type = ContractType.Receipt,
                    publishable = false,
                    rewardMultiplier = 1f,
                    timeLimitSeconds = 600f,
                    isReceiptContract = true,
                    localizationKey = "ui.contract.type.receipt",
                    uiClass = "type-receipt",
                    displayNameFallback = "[Расписка]",
                    uiColor = new Color(0.3f, 1f, 0.3f)
                }
            };

            return catalog;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (locations != null)
            {
                for (int i = 0; i < locations.Count; i++)
                {
                    if (locations[i] != null)
                        locations[i].locationId = MarketConfigCollector.NormalizeLocationId(locations[i].locationId);
                }
            }

            if (distances != null)
            {
                for (int i = 0; i < distances.Count; i++)
                {
                    if (distances[i] == null) continue;
                    distances[i].fromLocationId = MarketConfigCollector.NormalizeLocationId(distances[i].fromLocationId);
                    distances[i].toLocationId = MarketConfigCollector.NormalizeLocationId(distances[i].toLocationId);
                }
            }
        }
#endif
    }
}
