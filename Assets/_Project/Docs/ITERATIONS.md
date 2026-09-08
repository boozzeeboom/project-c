# Журнал итераций

## Итерация от 2026-09-08 (T-JITTER18)

**Задача:** Подключить character-visual эксперимент к настоящей игре/билду вместо ограниченного стенда и проверить его на реальной проблемной дистанции.
**Результат:** Near-origin skinning копии текущей локальной позы был подключён к реальному `NetworkPlayer`. В пользовательском Play Mode персонаж был проверен на координатах примерно `(39877.62, 2502.17, 40026.44)` — около 56,5 км от Unity-origin. Персонажа продолжает трясти. Эксперимент **не устраняет микроджиттер**. Кодовый эксперимент откатан к baseline `57572dc006381f9523ed8d3c7d6a68e2baf7551f`; актуальная документация сохранена.
**Изменения:**
- Временно добавленные/изменённые файлы T-JITTER18 удалены или восстановлены к baseline: `CharacterLocalSkinningExperiment.cs`, `NetworkPlayer.cs`, `EdgeDetectionRenderFeature.cs`.
- `docs/Character/IMPLEMENTATION_CHARACTER_LOCAL_SKINNING_EXPERIMENT.md` — зафиксированы runtime FAIL и выполненный откат.
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER_SOLUTIONS_RESEARCH.md` — зафиксирован отрицательный результат D1/D2 и остановка эксперимента.
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — актуальный статус обновлён, исторические измерения сохранены.
- `Assets/_Project/Docs/ITERATIONS.md` — эта запись.
**Проверки:** Unity compile до пользовательского прогона был PASS («No compile errors»). Runtime-проверка выполнена в реальной игре на проблемной дальней позиции; активация эксперимента подтверждалась, но визуальный джиттер сохранился. Итог теста: **FAIL**. Скриншоты/видео пользователем не предоставлялись; дополнительные регрессионные проверки после отрицательного результата не выполнялись.
**Сравнение:** проверка проводилась на штатном пути с включённым T-JITTER18; валидация показала, что локализация skinning через proxy не меняет наблюдаемый симптом на дальней позиции. Аргумент `-disableCharacterLocalSkinning` после решения об откате больше не нужен для сохранения текущего baseline.
**Откат:** код восстановлен из `57572dc006381f9523ed8d3c7d6a68e2baf7551f` без `reset --hard` и без `rebase`. Несвязанные изменения пользователя сохранены и не включаются в этап.

---

## Итерация от 2026-09-08 (T-JITTER17)

**Задача:** Задокументировать актуальную архитектуру без FloatingOrigin, проверить готовые anti-jitter решения и ограничить область исправления персонажем.
**Результат:** Docs-only этап D0 завершён. Исследованы Origin Shift, Floating Origin Network, World Streamer 2 и SAM. Готовый plug-and-play фикс для Unity 6000.5.2f1 / URP 17.5 / NGO 2.13 и нашего Humanoid не подтверждён. Масштабная миграция координат мира отложена; предложен один изолированный visual-only эксперимент A/B/C с явными условиями PASS/STOP. Эксперимент не реализован и не запускался.
**Изменения:**
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER_SOLUTIONS_RESEARCH.md` — новый анализ, первоисточники, оценка пакетов и ограниченный порядок следующих этапов.
- `docs/Character/INVESTIGATION_CHARACTER_MICRO_JITTER.md` — ссылка на T-JITTER17 и отметка, что исторический §9.5 не является текущим планом внедрения; результаты прошлых тестов сохранены.
- `Assets/_Project/Docs/ITERATIONS.md` — запись этого этапа в существующем журнале T-JITTER; новый журнал не создавался.
**Проверки:** только документация; код, сцены, префабы, настройки и сохранения не изменялись. Пакеты не покупались/не импортировались. Play Mode и скриншоты не выполнялись; исправление симптома не заявляется. Перед docs-коммитом проверяется точный состав diff и `git diff --check`.
**Следующий этап:** только после согласования — отдельный character-visual стенд, затем пользовательский прогон; не автоматическая перестройка MMO.

---

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
**Коммит:** `2f5191ae8917065dd421b7ceb8ca893f8c035644` — T-JITTER16: runtime vertex probe и последующая очистка  
**Результат:** Подтверждено: на `distOrigin≈56493м` local baked-vertex deltas вырастают примерно в 3–5 раз относительно origin. `local` и `relative-world` совпадают, значит источник до камеры и `NetworkTransform` — в humanoid deformation/skinning-пути при больших абсолютных координатах.
**Изменения:**
- Создан и после теста удалён временный `SkinnedVertexRuntimeProbe`.
- Удалены временные `BoneJitterRuntimeProbe`, `JitterClipProbe`, `InvestigateAnimator`.
- Компонент `ProjectC.DebugTools.SkinnedVertexRuntimeProbe` удалён из `NetworkPlayer.prefab`.
- T-JITTER15 (`skinnedMotionVectors=false` после body-swap) откатан: симптом не изменился.
- Compile check после очистки: ошибок нет.

**Историческое архитектурное решение (отложено в T-JITTER17, 2026-09-08):** перейти к local-coordinate слою для MMO: `SceneID/ChunkID + localPosition`, чтобы humanoid и физика работали рядом с Unity origin; глобальные координаты не хранить в одном float `Transform.position`. Это больше не обязательный следующий шаг устранения текущего визуального бага: сначала ограниченный character-visual эксперимент, см. запись T-JITTER17 выше.

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

