# Crafting System — Итерации разработки

---

## Итерация от 2026-07-09

**Задача:** Исправление 5 критических багов (B1-B5) и 7 техдолгов (T1-T7) по плану AUDIT_2026-07-09.md

**Коммит:** `0389336c293f8a4a6eb8f446532e3491977fece8` — T-CRAFT01: исправление 5 критических багов и 7 техдолгов

**Изменения:**
- `Assets/_Project/Scripts/Crafting/CraftingServer.cs` — B1, B2, B3, B4, B5, T4, T5, T6
- `Assets/_Project/Scripts/Crafting/CraftingStation.cs` — B2, T2
- `Assets/_Project/Scripts/Crafting/CraftingWorld.cs` — T1, T2
- `Assets/_Project/Scripts/Crafting/CraftingClientState.cs` — T3 (клиентский кеш)
- `Assets/_Project/Scripts/Crafting/UI/CraftingWindow.cs` — T3 (отвязка от CraftingWorld)
- `Assets/_Project/Scripts/Crafting/Dto/CraftingDtos.cs` — T7 (удалён)
- `docs/Crafting_system/99_CHANGELOG.md` — запись v0.1.1-fixes-session1+2

**Статус:** ✅ Компиляция 0 errors
