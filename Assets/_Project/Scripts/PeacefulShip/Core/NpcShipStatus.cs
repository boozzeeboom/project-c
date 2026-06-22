// T-NS00: NpcShipStatus — серверная FSM для NPC-корабля.
// Pattern: QuestState (Quests/Core/QuestInstance.cs), DockingStatus (Docking/Dto/DockingDto.cs:104).
// Convention: public enum X : byte (per project AGENTS.md / project-c-bootstrap).

namespace ProjectC.PeacefulShip.Core
{
    /// <summary>
    /// Сервер-авторитативное состояние NPC-корабля. Owned by NpcShipWorld.
    /// 12 состояний FSM (см. docs/NPC_others_peacfull/pc_ship/04_LIVING_BEHAVIOR.md §2).
    /// </summary>
    public enum NpcShipStatus : byte
    {
        Idle,           // 0 — только что заспавнен, ждёт первого Departing
        Departing,      // 1 — съезд с пада + набор высоты (5 сек anti-grav boost, Q8)
        InTransit,      // 2 — полёт к целевой станции
        Approaching,    // 3 — заход на посадку (dist < 500m)
        Holding,        // 4 — ждёт свободный pad (5 сек retry)
        Diverting,      // 5 — уходит к другой станции (pad занят / timeout)
        Docking,        // 6 — финальное позиционирование на pad
        Docked,         // 7 — на паде, двигатель заблокирован (ShipController.EnterDocked)
        Loading,        // 8 — 30-90 сек no-op пауза (Q5)
        Undocking,      // 9 — отстыковка (ShipController.ExitDocked)
        Done            // 10 — цикл завершён, restart
    }
}