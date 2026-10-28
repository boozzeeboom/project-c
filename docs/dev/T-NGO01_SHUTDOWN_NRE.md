# T-NGO01 — NRE при завершении Play Mode в Netcode for GameObjects

> **Дата:** 2026-10-28
> **Статус:** **НЕ РАЗОБРАНО.** Нужен отдельный разбор. Ничего не исправлено, ни один файл NGO не менялся.
> **Пакет:** `com.unity.netcode.gameobjects` **2.13.0** (`Library/PackageCache/com.unity.netcode.gameobjects@0f12e689d980`)
> **Среда:** Unity 6000.5.2f1, Windows, редактор

---

## 1. Симптом

После выхода из Play Mode в консоль попадают две `NullReferenceException` (сняты из консоли редактора, `read_console`, стек дословный):

```
NullReferenceException: Object reference not set to an instance of an object
  Unity.Netcode.NetworkSceneManager.Dispose ()            (NetworkSceneManager.cs:610)
  Unity.Netcode.NetworkManager.ShutdownInternal ()         (NetworkManager.cs:1672)
  Unity.Netcode.NetworkManager.OnDestroy ()                (NetworkManager.cs:1754)
  Unity.Netcode.NetworkManager.OnApplicationQuit ()        (NetworkManager.cs:1740)
```

```
NullReferenceException: Object reference not set to an instance of an object
  Unity.Netcode.NetworkSpawnManager.DespawnAndDestroyNetworkObjects () (NetworkSpawnManager.cs:1608)
  Unity.Netcode.NetworkManager.ShutdownInternal ()         (NetworkManager.cs:1659)
  Unity.Netcode.NetworkManager.OnDestroy ()                (NetworkManager.cs:1754)
```

Обе записи создаёт **сам NGO**: `OnDestroy()` ловит исключение и логирует его (`NetworkManager.cs:1752–1759`, строка лога `1758`). Наружу ничего не пробрасывается, но проход `ShutdownInternal` на этом месте **обрывается**.

## 2. К меню не относится

- В обеих стеках **нет ни одного кадра меню** (`Assets/Aura/Mods/ProjectCMainMenu/**`).
- Код меню не вызывает teardown netcode: в `Assets/Aura/Mods/ProjectCMainMenu/Scripts/*.cs` нет ни `Shutdown()`, ни `Singleton.Shutdown`, ни подписок на `OnPreShutdown` / `OnStopped`.
- Единственная связь — меню **запускает сессию**: `ProjectCMainMenuUiHost.OnMenuAction("host")` → `StartHost()` → `GlobalMotionPilotRuntime.StartPilotHost()`. Любой другой лаунчер сессии (`MainMenuWindow.OnHostClicked`, `NetworkTestMenu`) дал бы тот же лог.
- Исключения возникают на тирдауне NGO при любом активном слушающем `NetworkManager`, а не от UI Toolkit, USS, рампов и облаков меню.

**Вывод:** это дефект жизненного цикла NGO в связке с настройками редактора проекта, а не регрессия меню.

## 3. Что уже установлено (факты, не гипотезы)

1. `ShutdownInternal` вызван **дважды**. Порядок внутри него: `SpawnManager?.DespawnAndDestroyNetworkObjects()` — строка **1659**, `SceneManager?.Dispose()` — строка **1672**. Каждое исключение обрывает проход на своей строке, поэтому **один** проход физически не может дать обе трассы.
2. У второй трассы **нет кадра `OnApplicationQuit`**, у первой — есть. Значит входы разные. Второй вход — либо движковый `OnDestroy` NetworkManager-а при выходе из Play Mode, либо `OnSceneUnloaded` (`NetworkManager.cs:1607–1613`, там `OnDestroy()` вызывается вручную).
3. В `ShutdownInternal` **нет защиты от повторного входа**; `m_ShuttingDown` сбрасывается в `false` прямо внутри (`1678`, `1690`). Ранний выход есть только в `OnApplicationQuit` — `if (Singleton == null && !IsListening) return;` (`1736`), а `IsListening = false` выставляется лишь в `1677`. Обрыв раньше `1677` оставляет `IsListening == true` и `Singleton != null`, поэтому следующий вход выполняет **полный** тирдаун на полуразобранном менеджере.
4. `EditorSettings.asset`: `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 3` — **DisableDomainReload | DisableSceneReload**. При выходе из Play Mode к одному и тому же `NetworkManager` приходят несколько точек тирдауна подряд, и статические состояния между запусками не пересоздаются.
5. `NetworkManager.PrefabHandler` **не может быть null**: это ленивый геттер, он создаёт `NetworkPrefabHandler` при первом обращении (`NetworkManager.cs:942–954`), и `m_PrefabHandler` нигде не обнуляется. Значит null в трассе B — **не** `PrefabHandler`; он внутри обработки `DespawnAndDestroyNetworkObjects` (строка `1608` — место вызова).
6. `SceneManager` (`NetworkManager.cs:992`) присваивается только в `Initialize()` (`1277`) и обнуляется только в `1673`; `SceneEventDataStore` (`NetworkSceneManager.cs:566`) создаётся в конструкторе (`833`) и обнуляется **только** в `Dispose()` (`619`). Трасса A означает, что `Dispose()` был вызван на этом же экземпляре `NetworkSceneManager` **повторно**.

## 4. Что НЕ установлено

- **Точный путь обнуления.** Трасса A требует «`SceneManager` не null, но его `SceneEventDataStore` уже null», а единственный код, создающий такое состояние, — тот же `Dispose()` (`618–619`), после которого `SceneManager` сразу обнуляется (`1673`). Какой именно вход вызвал первый `Dispose()` — не найдено.
- Какие именно события тирдауна приходят в каком порядке в этом проекте (движковый `OnDestroy` / `OnApplicationQuit` / `sceneUnloaded`) — не протоколировано.
- Воспроизводимость: не проверялось, повторяется ли это на каждой сессии и зависит ли от того, был ли выгружен/загружен `WorldScene_0_0` (пилот грузит её и «увольняет» прежних владельцев сцены — выгрузка сцены даёт `sceneUnloaded` → `OnSceneUnloaded` → `OnDestroy`).
- Не проверялось, исправлено ли это в более новой версии NGO 2.x.

## 5. Почему это не безобидно

Оба обрыва происходят **до** строк `1709–1723`, то есть `OnClientStopped`, `OnServerStopped` и `OnStopped` при выходе из Play Mode **не вызываются**. На них есть подписчики в проекте, например `GlobalMotionCheckpointSpawnSource.OnStopped` → `RequestRetirement()` (`Assets/_Project/Scripts/World/FloatingOrigin/Persistence/GlobalMotionCheckpointSpawnSource.cs:62`). Плюс сам менеджер остаётся в полуразобранном состоянии: часть подсистем уже обнулена, а `IsListening`/`Singleton` — ещё нет.

## 6. Что проверить дальше (план, ещё не выполнялся)

1. Воспроизвести без меню: старт хоста из `MainMenuWindow.OnHostClicked` / `NetworkTestMenu` и выход из Play Mode — если лог тот же, меню окончательно исключено.
2. Обернуть разбор: залогировать в `NetworkManager.OnDestroy`/`OnApplicationQuit` (временный `#if`-патч локально, не в пакете) значения `IsListening`, `Singleton == this`, `SceneManager == null`, `SpawnManager == null` — это сразу покажет, какой вход второй и в каком состоянии он приходит.
3. Проверить, вызывается ли `sceneUnloaded` при работе `GlobalMotionPilotRuntime.StartPilotHost()` (загрузка `WorldScene_0_0` и «retire» владельцев сцен).
4. Сравнить с `m_EnterPlayModeOptionsEnabled: 0` (с доменной перезагрузкой) — если исключения исчезают, подтверждается вклад настройки.
5. Проверить `CHANGELOG.md` NGO на исправление двойного `ShutdownInternal` в версиях > 2.13.0.

## 7. Что не тронуто

Код NGO в `Library/PackageCache` (пакет read-only), скрипты меню, `PCMM_Menu.uss`, `BootstrapScene.unity`, `WorldScene_0_0.unity`, материалы и шейдеры. Этот документ — единственное изменение.
