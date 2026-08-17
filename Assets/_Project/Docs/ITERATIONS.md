# Журнал итераций

## Итерация от 2026-08-17

**Задача:** Проверить гипотезу `skinnedMotionVectors` после body-swap персонажа (T-JITTER15)  
**Коммит:** `3b74ab5a8c5fd5312ad4d72b0fb69fe6f9931047` — T-JITTER15: диагностический фикс откатан после runtime-проверки  
**Результат:** Гипотеза не подтверждена — после отключения `skinnedMotionVectors` тряска сохранилась. Кодовый фикс откатан; запись оставлена как отрицательный результат теста.
**Изменения:**
- `Assets/_Project/Scripts/Player/CharacterCustomisationApplier.cs` — диагностическое изменение удалено
- Следующий шаг — runtime-зонд вершин через `SkinnedMeshRenderer.BakeMesh()`

---

## Итерация от 2026-08-17 (T-JITTER16)

**Задача:** Измерить baked-вершины персонажа в рантайме на origin и в WorldScene_0_0  
**Коммит:** `pending` — T-JITTER16: откат T-JITTER15 и добавление runtime vertex probe  
**Статус:** Диагностический зонд создан и добавлен в `NetworkPlayer.prefab`; требуется runtime playtest пользователя
**Изменения:**
- `Assets/_Project/Scripts/Debug/SkinnedVertexRuntimeProbe.cs` — измеряет local, relative-world и world deltas sampled vertices через `SkinnedMeshRenderer.BakeMesh()`
- `Assets/_Project/Prefabs/NetworkPlayer.prefab` — добавлен `ProjectC.DebugTools.SkinnedVertexRuntimeProbe` на root `NetworkPlayer`
- Compile check: ошибок нет

**Протокол:**
- Прогон A: персонаж около `(0, 3, 0)`
- Прогон B: персонаж после загрузки `WorldScene_0_0` около `(40000, 3000, 40000)`
- Сравнить строки `[SkinnedVertexProbe]` по `local max/rms`, `relative max/rms`, `world max/rms`

---

## Итерация от 2026-07-14

**Задача:** Исправить баг: при перезаходе теряется доступ к кораблю (ключ в инвентаре, но корабль заблокирован)  
**Коммит:** `4b95e65` — T-KEY-FIX: persistentShipId для KeyRodInstance — фикс потери доступа к кораблю между сессиями  
**Изменения:**
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstance.cs` — добавлено поле `persistentShipId`
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceRepository.cs` — `persistentShipId` в DTO и SaveAll
- `Assets/_Project/Scripts/Ship/Key/KeyRodInstanceWorld.cs` — индекс `_instancesByPersistentId`, rebind `registeredShipId` при спавне, очистка stale-инстансов
- `Assets/_Project/Scripts/Player/ShipController.cs` — `CreateKeyInstanceWhenReady` передаёт `ShipPersistentId`

**Корень бага:** `NetworkObjectId` нестабилен между сессиями → дубликаты `KeyRodInstance` → проверка владения находила новый instance с `owner=NONE`

---

## Итерация от 2026-07 (v2)
=======


**Задача:** Исправить микротряску персонажа при standing  
**Коммит:** `3866c59` — T-JITTER01-v2: корневая причина — NetworkTransform.Interpolate конфликтует с CharacterController.Move/NavMeshAgent  
**Изменения:**
- `Assets/_Project/Scripts/Player/NetworkPlayer.cs` — `OnNetworkSpawn`: `nt.Interpolate = false` для owner; `using Unity.Netcode.Components`; фильтрация sleeping Rigidbody + delta threshold в platform carry
- `Assets/_Project/Scripts/AI/NpcBrain.cs` — `OnNetworkSpawn`: `nt.Interpolate = false` на хосте; `using Unity.Netcode.Components`
- `Assets/_Project/Docs/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — полный v2-диагноз
- `Assets/_Editor/InvestigateAnimator.cs` — diagnostic tool (создан)

**Стратегия отката:** `git revert 3866c59`

## Итерация от 2026-08-14

**Задача:** Вынести номер версии в главном меню в поле инспектора (слова локализованы, цифры подставляются)
**Коммит:** `731db58` — T-UI03: версия в главном меню вынесена в поле инспектора
**Изменения:**
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — добавлено поле `versionText` (секция Version), подпись через `Loc.BindFormat`
- `Assets/_Project/Scripts/Localization/Loc.cs` — добавлен `BindFormat` + `FormatWithFallback`
- `Assets/_Project/Settings/Localization/UI_Table_ru.asset` — `ui.main_menu.subtitle` → `Версия Alpha {0}`
- `Assets/_Project/Settings/Localization/UI_Table_en.asset` — `ui.main_menu.subtitle` → `Alpha {0}`
- `Assets/_Project/Editor/Localization/AddMainMenuLocKeys.cs` — seed обновлён на `{0}`

---

## Итерация от 2026-08-15

**Задача:** Исправить persistence экипировки персонажа и убрать отладочную выдачу одежды при подключении  
**Коммит:** `aff555584b6a13ccce2d319a6a205b284b6efd9b` — T-EQP01: исправить persistence экипировки и убрать debug seed  
**Изменения:**
- `Assets/_Project/Scripts/Equipment/EquipmentServer.cs` — удалена hardcoded seed-выдача тестовых предметов; добавлена загрузка экипировки при подключении; equip/unequip теперь сохраняются сразу
- `Assets/_Project/Scripts/Stats/StatsServer.cs` — добавлена загрузка equipment из общего character persistence независимо от порядка спавна серверных объектов

---

## Итерация от 2026-08-16

**Задача:** Добавить в MainMenu debug-блок для удаления отдельных состояний persistence и всех игровых сохранений  
**Коммит:** `1a354294c7c0a1810180a5cb6c3d1fe752259dbc` — T-UI04: debug-очистка persistence в MainMenu  
**Изменения:**
- `Assets/_Project/Resources/UI/MainMenuWindow.uxml` — добавлен блок Debug слева сверху с кнопками очистки состояний
- `Assets/_Project/Resources/UI/MainMenuStyles.uss` — добавлены стили debug-панели
- `Assets/_Project/Scripts/UI/MainMenu/MainMenuWindow.cs` — подключены обработчики кнопок
- `Assets/_Project/Scripts/UI/MainMenu/PersistenceDebugTools.cs` — удаление JSON/TXT persistence и trade PlayerPrefs с сохранением настроек и input bindings

