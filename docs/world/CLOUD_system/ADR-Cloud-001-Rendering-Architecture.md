# ADR-Cloud-001: Cloud Rendering Architecture

**Date:** 3 мая 2026
**Status:** Accepted
**Deciders:** Technical Director, Unity Shader Specialist, Art Director

---

## Context

Текущая система облаков использует 890+ mesh-примитивов (сферы, плоскости, цилиндры), что создаёт:
- Плохую визуальную эстетику (не "вкусные" облака)
- Высокую нагрузку на draw calls (890+)
- Отсутствие объёма и реалистичного рендеринга
- Нет сетевой синхронизации для multiplayer

Требуется новая архитектура, которая:
1. Обеспечивает качественную визуальную картинку
2. Поддерживает server-authoritative wind/storms
3. Работает с 24-сценным streaming
4. Укладывается в performance budget (<3ms GPU)

---

## Decision

### Rendering: Hybrid Volumetric + Billboard System

**Выбрано:**
- **Upper layer (6000-8000m):** Billboard impostors (инстанced)
- **Middle layer (3000-5000m):** Simplified volumetric OR billboards
- **Lower layer (1500-3000m):** Fullscreen raymarch с analytical noise
- **Cumulonimbus storms:** Full volumetric + VFX Graph lightning

**Не выбрано:**
- VDB fog (слишком тяжёлый для MMO)
- 3D texture noise (избыточный memory cost)
- Pure mesh-based (текущий подход — отклонён)

### Networking: Hybrid Client-Server

**Выбрано:**
- Wind direction/speed: Server-authoritative, broadcast 0.5 Hz
- Storm positions: Server-authoritative, RPC on spawn/state change
- Decorative clouds: Client-side only, no sync
- Time of day: Server-authoritative

**Не выбрано:**
- Full cloud position sync (избыточный bandwidth)
- Client-driven wind (синхронизация сломается)

---

## Consequences

### Positive
- Качественные облака с volumetric rendering
- Низкий bandwidth (~58 B/s total)
- Масштабируемость на 64 игрока
- Cloud layers работают с 24-scene streaming

### Negative
- Raymarch требует GPU resources (но в budget)
- Более сложная реализация чем mesh-based
- Требуется URP ScriptableRenderPass

### Risks
- Raymarch shader complexity (mitigation: analytical noise, no 3D textures)
- Performance on low-end (mitigation: LOD, adaptive step count)
- Scene streaming integration (mitigation: event-based regeneration)

---

## Implementation Notes

1. **Start with billboard only** — prove concept before volumetric
2. **Analytical noise over 3D texture** — no memory overhead
3. **Server wind is critical path** — implement first for multiplayer
4. **Test each phase** — fail fast, iterate

---

## Related ADRs

- ADR-Cloud-002: Storm Network Protocol
- ADR-Cloud-003: Scene Distribution Strategy