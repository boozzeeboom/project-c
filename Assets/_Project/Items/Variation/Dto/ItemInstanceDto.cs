// =====================================================================================
// ItemInstanceDto.cs — сетевой DTO сгенерированного инстанса предмета (T-VAR01)
// =====================================================================================
// Документация: docs/pickble_items/variation_generator/02_API.md §4
//
// Назначение: передать клиенту всё, что нужно для карточки/тултипа/экипировки:
//   • instanceId (серверный id инстанса, 0 = ещё не зарегистрирован);
//   • базовые и итоговые статы (клиент не обязан иметь профиль и ItemData);
//   • роллы аффиксов (для аудита/отображения "почему такие статы");
//   • имя/тир/качество/цену.
//
// ВАЖНО: это отдельный DTO, InventoryItemDto не меняем — интеграция с инвентарём
// описана в docs/pickble_items/variation_generator/04_INTEGRATION.md и делается
// отдельным тикетом (нужен instanceId в слоте инвентаря).
// =====================================================================================

using System;
using Unity.Netcode;

namespace ProjectC.Items.Variation.Dto
{
    [Serializable]
    public struct ItemStatsDto : INetworkSerializable
    {
        public int damage;
        public int critChance;
        public int rangeMilli;
        public int weightMilli;
        public int price;
        public int durability;
        public int armor;
        public int strengthMilli;
        public int dexterityMilli;
        public int intelligenceMilli;
        public int stackSize;
        public int moveSpeed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref damage);
            serializer.SerializeValue(ref critChance);
            serializer.SerializeValue(ref rangeMilli);
            serializer.SerializeValue(ref weightMilli);
            serializer.SerializeValue(ref price);
            serializer.SerializeValue(ref durability);
            serializer.SerializeValue(ref armor);
            serializer.SerializeValue(ref strengthMilli);
            serializer.SerializeValue(ref dexterityMilli);
            serializer.SerializeValue(ref intelligenceMilli);
            serializer.SerializeValue(ref stackSize);
            serializer.SerializeValue(ref moveSpeed);
        }

        public static ItemStatsDto From(ItemVariationBaseStats stats)
        {
            return new ItemStatsDto
            {
                damage = stats.damage,
                critChance = stats.critChance,
                rangeMilli = stats.rangeMilli,
                weightMilli = stats.weightMilli,
                price = stats.price,
                durability = stats.durability,
                armor = stats.armor,
                strengthMilli = stats.strengthMilli,
                dexterityMilli = stats.dexterityMilli,
                intelligenceMilli = stats.intelligenceMilli,
                stackSize = stats.stackSize,
                moveSpeed = stats.moveSpeed,
            };
        }

        public ItemVariationBaseStats ToCore()
        {
            return new ItemVariationBaseStats
            {
                damage = damage,
                critChance = critChance,
                rangeMilli = rangeMilli,
                weightMilli = weightMilli,
                price = price,
                durability = durability,
                armor = armor,
                strengthMilli = strengthMilli,
                dexterityMilli = dexterityMilli,
                intelligenceMilli = intelligenceMilli,
                stackSize = stackSize,
                moveSpeed = moveSpeed,
            };
        }
    }

    [Serializable]
    public struct ItemStatRollDto : INetworkSerializable
    {
        public byte stat;
        public int flat;
        public int percentBps;
        public int modifierIndex;
        public int tierMultiplierBps;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref stat);
            serializer.SerializeValue(ref flat);
            serializer.SerializeValue(ref percentBps);
            serializer.SerializeValue(ref modifierIndex);
            serializer.SerializeValue(ref tierMultiplierBps);
        }

        public static ItemStatRollDto From(ItemStatRoll roll)
        {
            return new ItemStatRollDto
            {
                stat = (byte)roll.stat,
                flat = roll.flat,
                percentBps = roll.percentBps,
                modifierIndex = roll.modifierIndex,
                tierMultiplierBps = roll.tierMultiplierBps,
            };
        }

        public ItemStatRoll ToCore()
        {
            return new ItemStatRoll
            {
                stat = (ItemStatId)stat,
                flat = flat,
                percentBps = percentBps,
                modifierIndex = modifierIndex,
                tierMultiplierBps = tierMultiplierBps,
            };
        }
    }

    [Serializable]
    public struct ItemInstanceDto : INetworkSerializable
    {
        public int schemaVersion;
        public int instanceId;
        public int baseItemId;
        public int tierIndex;
        public int quality;
        public int finalPrice;
        public int priceMultiplierBps;
        public long seed;
        public int luckBps;

        public string profileId;
        public string baseItemName;
        public string displayName;
        public string tierId;
        public string tierName;
        public string tierColorHex;

        public ItemStatsDto baseStats;
        public ItemStatsDto stats;
        public ItemStatRollDto[] rolls;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref schemaVersion);
            serializer.SerializeValue(ref instanceId);
            serializer.SerializeValue(ref baseItemId);
            serializer.SerializeValue(ref tierIndex);
            serializer.SerializeValue(ref quality);
            serializer.SerializeValue(ref finalPrice);
            serializer.SerializeValue(ref priceMultiplierBps);
            serializer.SerializeValue(ref seed);
            serializer.SerializeValue(ref luckBps);

            SerializeString(serializer, ref profileId);
            SerializeString(serializer, ref baseItemName);
            SerializeString(serializer, ref displayName);
            SerializeString(serializer, ref tierId);
            SerializeString(serializer, ref tierName);
            SerializeString(serializer, ref tierColorHex);

            var baseStatsValue = baseStats;
            baseStatsValue.NetworkSerialize(serializer);
            baseStats = baseStatsValue;

            var statsValue = stats;
            statsValue.NetworkSerialize(serializer);
            stats = statsValue;

            int count = rolls != null ? rolls.Length : 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
                rolls = count > 0 ? new ItemStatRollDto[count] : Array.Empty<ItemStatRollDto>();
            for (int i = 0; i < count; i++)
            {
                var roll = rolls[i];
                roll.NetworkSerialize(serializer);
                rolls[i] = roll;
            }
        }

        public static ItemInstanceDto FromResult(ItemVariationResult result, int instanceId)
        {
            if (result == null) return default;

            var rolls = new ItemStatRollDto[result.rolls != null ? result.rolls.Count : 0];
            for (int i = 0; i < rolls.Length; i++)
                rolls[i] = ItemStatRollDto.From(result.rolls[i]);

            return new ItemInstanceDto
            {
                schemaVersion = result.schemaVersion,
                instanceId = instanceId,
                baseItemId = result.baseItemId,
                tierIndex = result.tierIndex,
                quality = result.quality,
                finalPrice = result.finalPrice,
                priceMultiplierBps = result.priceMultiplierBps,
                seed = result.seed,
                luckBps = result.luckBps,
                profileId = result.profileId,
                baseItemName = result.baseItemName,
                displayName = result.displayName,
                tierId = result.tierId,
                tierName = result.tierName,
                tierColorHex = result.tierColorHex,
                baseStats = ItemStatsDto.From(result.baseStats),
                stats = ItemStatsDto.From(result.finalStats),
                rolls = rolls,
            };
        }

        public ItemVariationResult ToResult()
        {
            var resultRolls = new System.Collections.Generic.List<ItemStatRoll>(rolls != null ? rolls.Length : 0);
            if (rolls != null)
            {
                for (int i = 0; i < rolls.Length; i++)
                    resultRolls.Add(rolls[i].ToCore());
            }

            return new ItemVariationResult
            {
                schemaVersion = schemaVersion,
                profileId = profileId,
                baseItemId = baseItemId,
                baseItemName = baseItemName,
                seed = seed,
                luckBps = luckBps,
                tierIndex = tierIndex,
                tierId = tierId,
                tierName = tierName,
                tierColorHex = tierColorHex,
                displayName = displayName,
                quality = quality,
                priceMultiplierBps = priceMultiplierBps,
                finalPrice = finalPrice,
                baseStats = baseStats.ToCore(),
                finalStats = stats.ToCore(),
                rolls = resultRolls,
            };
        }

        private static void SerializeString<T>(BufferSerializer<T> serializer, ref string value) where T : IReaderWriter
        {
            bool hasValue = !string.IsNullOrEmpty(value);
            serializer.SerializeValue(ref hasValue);
            if (hasValue)
            {
                if (serializer.IsReader) value = string.Empty;
                serializer.SerializeValue(ref value);
            }
            else if (serializer.IsReader)
            {
                value = null;
            }
        }
    }
}
