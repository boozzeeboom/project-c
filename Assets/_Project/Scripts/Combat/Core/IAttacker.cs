// Project C: Real-Time Combat Engine — T-RTC01
// IAttacker interface — generic abstraction for ANY entity that can attack.
// Design: docs/Character/Skills/real-time-combat/10_DESIGN.md §2.1.
//
// Anti-restrictive: движок НЕ знает о Player/Npc/Ship. Любой класс, реализующий
// этот интерфейс, может быть зарегистрирован в CombatServer и участвовать в combat.
// Расширение = новый класс, реализующий IAttacker. 0 изменений в ядре.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Combat.Core
{
    /// <summary>
    /// Что угодно, что может атаковать: Player, Npc, Ship, Building (турель), ...
    /// </summary>
    public interface IAttacker
    {
        /// <summary>World position (для distance check на сервере).</summary>
        Vector3 GetPosition();

        /// <summary>STR — damage bonus в формуле <c>baseAttack = roll + base + STR</c>.</summary>
        int GetStrength();

        /// <summary>DEX — hit chance modifier (см. <c>IRangePolicy.CalculateHitChance</c>).</summary>
        int GetDexterity();

        /// <summary>INT — skill effectiveness (future, после T-CB01..T-CB09).</summary>
        int GetIntelligence();

        /// <summary>Все активные источники урона (меч + щит-импульс + турели + ...).</summary>
        IReadOnlyList<IDamageSource> GetActiveDamageSources();

        /// <summary>Найти конкретный source по id (для RPC "use source #N").</summary>
        IDamageSource GetDamageSource(ulong sourceId);

        /// <summary>Alive (HP &gt; 0). Сервер — истина (через IDamageTarget.IsAlive).</summary>
        bool IsAlive();

        /// <summary>Player? (для UI/toast/loss-penalties; false для NPC/Ship).</summary>
        bool IsPlayer();

        /// <summary>Cooldown check: source может атаковать прямо сейчас?</summary>
        bool CanAttack(IDamageSource source, float now);

        /// <summary>Установить cooldown (после успешной атаки).</summary>
        void SetCooldown(IDamageSource source, float until);

        /// <summary>Уникальный id атакующего (clientId для player, NetworkObjectId для NPC/Ship).</summary>
        ulong GetAttackerId();
    }
}
