// =====================================================================================
// ItemVariationInstances.cs — реестр инстансов и серверный фасад (T-VAR01)
// =====================================================================================
// Документация: docs/pickble_items/variation_generator/02_API.md
//
// ItemInstanceRegistry — аналог KeyRodInstanceWorld, но для сгенерированных вариантов:
//   • instanceId > 0 однозначно определяет конкретный ролл;
//   • сервер создаёт инстанс, кладёт DTO клиенту, а по instanceId позже отдаёт статы;
//   • экспорт/импорт JSON для персистентности и для межсерверной передачи.
//
// ItemVariationService — то, что вызывает сервер:
//   RegisterProfileJson(...)  ← загрузка профилей из docs/pickble_items/.../profiles/*.json;
//   TryCreate(profileId, seed) ← создать предмет с вариацией статов;
//   TryGetInstance(instanceId) ← получить статы уже выданного предмета;
//   ExportInstancesJson()/ImportInstancesJson() ← сохранение/восстановление.
// =====================================================================================

using System;
using System.Collections.Generic;
using ProjectC.Items.Variation.Dto;

namespace ProjectC.Items.Variation
{
    public sealed class ItemInstance
    {
        public int instanceId;
        public ItemVariationResult result;
    }

    public sealed class ItemInstanceRegistry
    {
        private readonly Dictionary<int, ItemInstance> _byId = new Dictionary<int, ItemInstance>();

        public int NextId { get; private set; } = 1;
        public int Count => _byId.Count;
        public IReadOnlyCollection<ItemInstance> All => _byId.Values;

        /// <summary>Добавить инстанс. instanceId <= 0 → выдать следующий свободный.</summary>
        public int Add(ItemVariationResult result, int instanceId = 0)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            if (instanceId > 0 && _byId.ContainsKey(instanceId))
                instanceId = 0;

            if (instanceId <= 0)
            {
                while (_byId.ContainsKey(NextId)) NextId++;
                instanceId = NextId++;
            }
            else if (instanceId >= NextId)
            {
                NextId = instanceId + 1;
            }

            _byId[instanceId] = new ItemInstance { instanceId = instanceId, result = result };
            return instanceId;
        }

        public bool TryGet(int instanceId, out ItemInstance instance)
        {
            return _byId.TryGetValue(instanceId, out instance);
        }

        public bool Remove(int instanceId) => _byId.Remove(instanceId);

        public void Clear()
        {
            _byId.Clear();
            NextId = 1;
        }

        public void SetNextId(int nextId)
        {
            NextId = Math.Max(1, nextId);
        }
    }

    public sealed class ItemVariationService
    {
        private readonly Dictionary<string, ItemVariationProfile> _profiles =
            new Dictionary<string, ItemVariationProfile>(StringComparer.Ordinal);

        public ItemInstanceRegistry Instances { get; } = new ItemInstanceRegistry();

        public int ProfileCount => _profiles.Count;
        public int InstanceCount => Instances.Count;

        /// <summary>
        /// Зарегистрировать профиль. Ошибки структуры (нет тиров/аффиксов/пустой id) блокируют
        /// регистрацию; предупреждения не блокируют. Повторный id заменяет профиль (reload).
        /// </summary>
        public bool RegisterProfile(ItemVariationProfile profile, out string error)
        {
            error = null;
            if (profile == null)
            {
                error = "Профиль == null.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(profile.profileId))
            {
                error = "profileId пуст.";
                return false;
            }

            List<ItemVariationIssue> issues = ItemVariationProfileFactory.Validate(profile);
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].severity == ItemVariationSeverity.Error)
                {
                    error = issues[i].message;
                    return false;
                }
            }

            _profiles[profile.profileId] = profile;
            return true;
        }

        /// <summary>Загрузить профиль из JSON-документа (см. ItemVariationJson.ProfileDocument).</summary>
        public bool RegisterProfileJson(string json, out string error)
        {
            if (!ItemVariationJson.TryProfileFromJson(json, out ItemVariationProfile profile, out error))
                return false;
            return RegisterProfile(profile, out error);
        }

        public bool TryGetProfile(string profileId, out ItemVariationProfile profile)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                profile = null;
                return false;
            }
            return _profiles.TryGetValue(profileId, out profile);
        }

        /// <summary>Создать предмет с вариацией статов и зарегистрировать инстанс.</summary>
        public bool TryCreate(
            string profileId,
            long seed,
            out ItemInstanceDto dto,
            out string error,
            int luckBps = 0)
        {
            dto = default;
            if (!TryCreateRaw(profileId, seed, out ItemVariationResult result, out int instanceId, out error, luckBps))
                return false;

            dto = ItemInstanceDto.FromResult(result, instanceId);
            return true;
        }

        public bool TryCreateRaw(
            string profileId,
            long seed,
            out ItemVariationResult result,
            out int instanceId,
            out string error,
            int luckBps = 0)
        {
            result = null;
            instanceId = 0;

            if (!_profiles.TryGetValue(profileId ?? string.Empty, out ItemVariationProfile profile))
            {
                error = $"Профиль '{profileId}' не зарегистрирован.";
                return false;
            }

            if (!ItemVariationGenerator.TryRoll(profile, seed, out result, out error, luckBps))
                return false;

            instanceId = Instances.Add(result);
            return true;
        }

        public bool TryGetInstance(int instanceId, out ItemInstanceDto dto)
        {
            dto = default;
            if (!Instances.TryGet(instanceId, out ItemInstance instance) || instance.result == null)
                return false;
            dto = ItemInstanceDto.FromResult(instance.result, instance.instanceId);
            return true;
        }

        public bool TryGetInstanceResult(int instanceId, out ItemVariationResult result)
        {
            result = null;
            if (!Instances.TryGet(instanceId, out ItemInstance instance)) return false;
            result = instance.result;
            return result != null;
        }

        public void ClearInstances() => Instances.Clear();

        public string ExportInstancesJson(bool prettyPrint = true)
        {
            var list = new List<ItemInstanceDto>(Instances.Count);
            foreach (ItemInstance instance in Instances.All)
                list.Add(ItemInstanceDto.FromResult(instance.result, instance.instanceId));

            list.Sort((a, b) => a.instanceId.CompareTo(b.instanceId));
            var snapshot = new ItemInstanceRegistrySnapshot
            {
                schemaVersion = ItemVariationJson.CurrentSchemaVersion,
                nextId = Instances.NextId,
                instances = list.ToArray(),
            };
            return ItemVariationJson.ToJson(snapshot, prettyPrint);
        }

        /// <summary>Восстановить инстансы из JSON. Текущие инстансы заменяются.</summary>
        public bool ImportInstancesJson(string json, out string error)
        {
            if (!ItemVariationJson.TryRegistryFromJson(json, out ItemInstanceRegistrySnapshot snapshot, out error))
                return false;

            Instances.Clear();
            if (snapshot.instances != null)
            {
                for (int i = 0; i < snapshot.instances.Length; i++)
                {
                    ItemInstanceDto dto = snapshot.instances[i];
                    if (dto.instanceId <= 0)
                    {
                        error = $"Инстанс [{i}] имеет instanceId <= 0.";
                        Instances.Clear();
                        return false;
                    }
                    if (Instances.TryGet(dto.instanceId, out _))
                    {
                        error = $"Инстанс [{i}] дублирует instanceId {dto.instanceId}.";
                        Instances.Clear();
                        return false;
                    }
                    ItemsVariationDtoConversion.AddDto(Instances, dto);
                }
            }

            Instances.SetNextId(Math.Max(snapshot.nextId, HighestId(snapshot) + 1));
            return true;
        }

        private static int HighestId(ItemInstanceRegistrySnapshot snapshot)
        {
            int max = 0;
            if (snapshot.instances == null) return max;
            for (int i = 0; i < snapshot.instances.Length; i++)
                if (snapshot.instances[i].instanceId > max) max = snapshot.instances[i].instanceId;
            return max;
        }
    }

    [Serializable]
    public class ItemInstanceRegistrySnapshot
    {
        public int schemaVersion = ItemVariationJson.CurrentSchemaVersion;
        public int nextId = 1;
        public ItemInstanceDto[] instances;
    }

    internal static class ItemsVariationDtoConversion
    {
        public static void AddDto(ItemInstanceRegistry registry, ItemInstanceDto dto)
        {
            registry.Add(dto.ToResult(), dto.instanceId);
        }
    }
}
