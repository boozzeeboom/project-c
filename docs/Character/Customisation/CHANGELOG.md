# Changelog — Character Customisation

> **Подсистема:** Character Customisation (additive layer поверх Stats / Equipment / Skills)
> **Формат:** Дата — Версия — Что изменилось — Тикеты
> **Дата создания:** 2026-06-30

---

## Дата | Сессия | Изменения

| Дата | Сессия | Что сделано | Тикеты |
|---|---|---|---|
| 2026-06-30 | (design-only) | Создан каталог `docs/Character/Customisation/`. Написан системный анализ кастомизации персонажа игроком — от L1 (М↔Ж) до L5 (лицо). Документы: 00_OVERVIEW (TL;DR + принципы), 01_CURRENT_CAPABILITIES (что уже есть), 02_DATA_MODEL (CustomisationSave + DTO + ClientState + Applier), 03_LEVELS_OF_CUSTOMISATION (L1..L5 с трудоёмкостью), 04_MALE_FEMALE_SWAP (глубокий разбор M↔F swap с кодом), 05_PHASES_ROADMAP (тикеты T-CUS-01..12 в 3 milestone'ах). **Код НЕ написан — design-only сессия.** | — |
| 2026-06-30 | (design-only) | **Ключевые находки:** (1) HumanF_Model.fbx уже в проекте (`Assets/Kevin Iglesias/Human Animations/Models/`). (2) Female animation set полный (Walk/Run/Sprint/Jump/Idle/etc.) с теми же именами что Male → AnimatorOverrideController подменяется trivially. (3) Skeleton generic Humanoid — EquipSlotToBone + Equipment Visual работают для обоих полов без изменений. (4) CharacterEquipmentVisualApplier уже на префабе, паттерн 100% переиспользуем. (5) SkillAnimationPlayer использует runtimeAnimatorController как base — работает прозрачно с F-версией. (6) CharacterSaveData + JsonCharacterDataRepository уже готовы к additive-расширению. **Итог: L1 (M↔F) — 3-5 дней, ~10-15 новых файлов, нулевой риск regression.** | — |

---

## Решения / контекст

| Дата | Решение | Контекст |
|---|---|---|
| 2026-06-30 | Кастомизация — отдельный orthogonal слой, не трогаем Stats/Equipment/Skills | Add-only паттерн по аналогии с EquipmentVisual |
| 2026-06-30 | CustomisationSave лежит в CharacterSaveData (additive) | Не создаём новый файл — JsonUtility backward-compat |
| 2026-06-30 | L1 приоритет, L4 второй, L3 третий (transform.localScale) | L2/L5 отложены (нужны доп. ассеты или SDK) |
| 2026-06-30 | Multiplayer sync — Variant A (client-only) для MVP, Variant B (NetworkVariable) — когда потребуется | Минимизация изменений в сетевом коде |
| 2026-06-30 | AnimatorOverrideController для F создаётся через drag-and-drop или Editor script | НЕ дублируем стейт-машину, только подменяем motion-ы |
| 2026-06-30 | Слайдеры тела через transform.localScale, не через blend shapes | Kevin Iglesias FREE не имеет blend shapes; transform.localScale — универсальное решение |

---

## Lessons learned

_(будет заполняться по мере реализации)_

---

## Open questions

_(см. `05_PHASES_ROADMAP.md` §8)_

1. Будет ли Character Creation Screen?
2. Будет ли cosmetic-only inventory?
3. Будет ли Faction-locked cosmetic?
4. Что с NPC customisation?
5. Что с Cinematic камерами (lighting для новых skin tones)?