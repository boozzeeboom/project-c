// T-NS M3.1: NavMode — 7 чистых режимов навигации NPC-корабля.
// Каждый режим = ОДИН конкретный input pattern. Нет смешивания yaw+thrust+vertical.
// Trigger-зоны (OuterCommZone, DockingPadTriggerBox) — единственный источник истины о прибытии.
//
// Документация: docs/NPC_others_peacfull/pc_ship/M2_FSM_DIAGNOSIS.md §3.2

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Чистые режимы навигации NPC-корабля. Один режим = один набор input'ов.
    /// Выбирается через spatial conditions (Vector3.Distance, углы, IsShipInside) — НЕ magic numbers.
    /// </summary>
    public enum NavMode : byte
    {
        /// <summary>Стоит на паде, двигатель заблокирован. Ждёт ExitDocked() для старта.</summary>
        Docked = 0,

        /// <summary>Вертикальный набор высоты (vertical input ONLY).</summary>
        /// <remarks>Условие завершения: pos.y >= startY + NavConfig.liftClearanceMeters (5м).</remarks>
        Lifting = 1,

        /// <summary>Поворот на месте к цели (yaw input ONLY).</summary>
        /// <remarks>Условие завершения: |bearing| < 5° (hysteresis от 15° входа).</remarks>
        Yawing = 2,

        /// <summary>Полёт к цели: thrust + vertical по прямой A→B (диagonal).</summary>
        /// <remarks>Условие завершения: dist < outerCommZone.commRange.</remarks>
        Cruising = 3,

        /// <summary>Hover у зоны связи, ждёт pad от диспетчера (DockingWorld.AssignPadForNpc).</summary>
        /// <remarks>Условие завершения: AssignedPadId != null.</remarks>
        Holding = 4,

        /// <summary>Финальный подход к паду: yaw → diagonal → vertical descent.</summary>
        /// <remarks>Условие завершения: DockingPadTriggerBox.IsShipInside → EnterDocked().</remarks>
        Berthing = 5,

        /// <summary>Только anti-gravity, нулевые input. Отладка/пауза (после ExitDocked пока не Lifting).</summary>
        Hover = 6,
    }
}
