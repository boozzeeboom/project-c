// Project C: Real-Time Combat Engine — T-RTC08
// DamageResultDto: INetworkSerializable структура для передачи DamageResult по сети.
// Design: docs/Character/Skills/real-time-combat/20_TECHNICAL.md §2.3.
//
// Server считает DamageResult → конвертирует в DamageResultDto → broadcast →
// client конвертирует обратно в DamageResult (или использует поля DTO напрямую для UI).

using Unity.Netcode;
using UnityEngine;

namespace ProjectC.Combat.Network
{
    public struct DamageResultDto : INetworkSerializable
    {
        public int baseAttack;
        public int diceRoll;            // P10
        public int strengthContribution;// P10
        public int baseContribution;    // P10
        public float locMult;
        public float critMult;
        public float skillMult;
        public float hitChance;
        public int preDefenseDamage;
        public int effectiveDefense;
        public int finalDamage;
        public bool isCrit;
        public bool isHit;
        public byte hitLocation;
        public byte damageType;
        public ulong attackerId;
        public ulong targetId;
        public ulong sourceId;
        public Vector3 attackerPosition;
        public Vector3 targetPosition;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref baseAttack);
            serializer.SerializeValue(ref diceRoll);
            serializer.SerializeValue(ref strengthContribution);
            serializer.SerializeValue(ref baseContribution);
            serializer.SerializeValue(ref locMult);
            serializer.SerializeValue(ref critMult);
            serializer.SerializeValue(ref skillMult);
            serializer.SerializeValue(ref hitChance);
            serializer.SerializeValue(ref preDefenseDamage);
            serializer.SerializeValue(ref effectiveDefense);
            serializer.SerializeValue(ref finalDamage);
            serializer.SerializeValue(ref isCrit);
            serializer.SerializeValue(ref isHit);
            serializer.SerializeValue(ref hitLocation);
            serializer.SerializeValue(ref damageType);
            serializer.SerializeValue(ref attackerId);
            serializer.SerializeValue(ref targetId);
            serializer.SerializeValue(ref sourceId);
            serializer.SerializeValue(ref attackerPosition);
            serializer.SerializeValue(ref targetPosition);
        }

        public static DamageResultDto FromResult(in Core.DamageResult r)
        {
            return new DamageResultDto
            {
                baseAttack = r.baseAttack,
                diceRoll = r.diceRoll,
                strengthContribution = r.strengthContribution,
                baseContribution = r.baseContribution,
                locMult = r.locMult,
                critMult = r.critMult,
                skillMult = r.skillMult,
                hitChance = r.hitChance,
                preDefenseDamage = r.preDefenseDamage,
                effectiveDefense = r.effectiveDefense,
                finalDamage = r.finalDamage,
                isCrit = r.isCrit,
                isHit = r.isHit,
                hitLocation = r.hitLocation,
                damageType = (byte)r.damageType,
                attackerId = r.attackerId,
                targetId = r.targetId,
                sourceId = r.sourceId,
                attackerPosition = r.attackerPosition,
                targetPosition = r.targetPosition,
            };
        }
    }
}
