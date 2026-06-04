# Commit Summary — Ghost Player Clone Fix

**Branch:** (твоя feature-ветка)
**Date:** 2026-06-04
**Author:** Mavis (Mavis) for Project C

---

## Commit message (готово к `git commit`)

```
fix(network): resolve ghost player clone on host (4-layer fix)

Хост спавнил двух NetworkPlayer'ов с одним OwnerClientId=0, что давало
видимого "клона" с инвертированным управлением, дубль InventoryUI и
гонку за камеру. Кроме того, real player падал с (0,3,0) под гравитацию
после StartHost, а на префабе NetworkPlayer.prefab не была выставлена
ссылка на камеру — игрок был невидим.

Root cause (4 независимых слоя):

  L1 NetworkPlayerSpawner.Update() руками спавнил свой GameObject как
     PlayerObject через SpawnAsPlayerObject(0) — конфликт с NGO
     PlayerPrefab auto-spawn. Дублировал реального игрока для хоста.
  L2 scene-placed PlayerSpawner на хосте имеет IsOwner=true
     (OwnerClientId=0=LocalClientId — server-owned), но НЕ является
     PlayerObject'ом. Без guard'а NetworkPlayer.OnNetworkSpawn запускал
     SpawnCamera + SpawnInventory и для пустышки → дубль InventoryUI и
     гонка камер.
  L3 ClientSceneLoader искал local player через FindGameObjectWithTag
     ("Player") и FindObjectsByType<NetworkPlayer>().First(IsOwner) —
     оба попадали на scene-placed первым, телепорт уходил не на того.
     Real player оставался на (0,3,0) и проваливался.
  L4 На NetworkPlayer.prefab поле cameraPrefab было {fileID: 0} (NULL)
     — scene override в BootstrapScene перезаписывал только scene-placed
     инстанс, не auto-spawn clone. После L1+L2+L3 камера не создавалась
     вообще, игрок невидим.

Fix:

  L1 NetworkPlayerSpawner.cs — убран Update() host-spawn loop и
     SpawnPlayerForClient (NGO PlayerPrefab auto-spawn сам всё делает).
     Компонент оставлен как diagnostic-only: логирует auto-spawn,
     помечает useScenePlayerAsHost как [Obsolete] для совместимости
     сериализованной сцены.
  L2 NetworkPlayer.cs — guard в OnNetworkSpawn / OnNetworkDespawn по
     наличию компонента NetworkPlayerSpawner на GameObject. Это
     надёжный дискриминатор (IsPlayerObject timing-unsafe в
     OnNetworkSpawn, см. ниже). Scene-placed отключается, real player
     живёт.
  L3 ClientSceneLoader.cs — helper FindRealLocalPlayerGameObject()
     опирается на NetworkManager.ConnectedClients[LocalClientId]
     .PlayerObject (source of truth от NGO). Все 4 call site'а
     (UpdatePlayerTransformAfterSpawn, FindLocalPlayer, WaitForPlayer,
     AutoLoadInitialSceneCoroutine) пробуют helper первым, fallback
     с фильтром IsOwner && IsPlayerObject.
  L4 NetworkPlayer.prefab — выставлены cameraPrefab → ThirdPersonCamera
     .prefab (guid 020b4cd7c3349134b8c1de87bed1f706) и walkSpeed
     5000 → 5 (была аномалия, scene override тоже ставил 5).

Verification (live, unityMCP manage_scene):
  - ровно один NetworkPlayer(Clone) с IsOwner=true
  - ровно одна ThirdPersonCamera_0, position совпадает с игроком
  - ровно один InventoryUI
  - scene-placed PlayerSpawner отключён (enabled=false на NetworkPlayer)
  - WASD управляет одним игроком, камера следит, инвертирования нет

LESSON (NGO 2.x footgun, cross-project):
  На хосте server-owned scene-placed NetworkObject имеет IsOwner=true
  (OwnerClientId=0=LocalClientId). Использовать IsPlayerObject как
  дискриминатор для player-specific логики, но НЕ во время
  OnNetworkSpawn (timing race для auto-spawned префаба). Source of
  truth: NetworkManager.ConnectedClients[LocalClientId].PlayerObject.
  Detail: docs/dev/INVESTIGATION_GHOST_PLAYER_CLONE.md

Files changed:
  - Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs (rewritten)
  - Assets/_Project/Scripts/Player/NetworkPlayer.cs (guards added)
  - Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs (helper + 4 sites)
  - Assets/_Project/Prefabs/NetworkPlayer.prefab (cameraPrefab, walkSpeed)
  - docs/dev/INVESTIGATION_GHOST_PLAYER_CLONE.md (new)
  - docs/dev/COMMIT_2026-06-04_GHOST_CLONE_FIX.md (this file, new)
```

---

## Готовые однострочные варианты subject'а (выбери любой)

Если не хочешь длинный commit message, можно так:

**Короткий (один subject + body ссылка):**
```
fix(network): resolve ghost player clone on host

4-layer fix: NetworkPlayerSpawner (L1) + NetworkPlayer guard (L2) +
ClientSceneLoader source-of-truth (L3) + NetworkPlayer.prefab
cameraPrefab (L4). См. docs/dev/INVESTIGATION_GHOST_PLAYER_CLONE.md
```

**Средний (subject + краткий body):**
```
fix(network): resolve ghost player clone on host (4-layer fix)

Хост спавнил двух NetworkPlayer'ов с одним OwnerClientId, плюс
real player падал с (0,3,0) и был невидим без cameraPrefab на префабе.
- L1: убран ручной SpawnAsPlayerObject в NetworkPlayerSpawner.Update
- L2: guard в OnNetworkSpawn по NetworkPlayerSpawner компоненту
- L3: ClientSceneLoader использует NGO PlayerObject (source of truth)
- L4: NetworkPlayer.prefab получает cameraPrefab + walkSpeed 5000→5
```

**Conventional Commits с типом:**
```
fix(network,scene,prefab): resolve ghost player clone on host
```

---

## Файлы для коммита (git add)

```bash
git add \
  Assets/_Project/Scripts/Network/NetworkPlayerSpawner.cs \
  Assets/_Project/Scripts/Player/NetworkPlayer.cs \
  Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs \
  Assets/_Project/Prefabs/NetworkPlayer.prefab \
  docs/dev/INVESTIGATION_GHOST_PLAYER_CLONE.md \
  docs/dev/COMMIT_2026-06-04_GHOST_CLONE_FIX.md
```

---

## Breaking changes / Migration notes

**Нет breaking changes.** Изменения:
- Поведение scene-placed `PlayerSpawner` (отключён) — не аффектит gameplay
- `NetworkConfig.PlayerPrefab` не менялся
- `NetworkPlayer.prefab` `cameraPrefab` теперь не NULL (был NULL — клиенты с override-only
  ломались) — это bugfix
- `walkSpeed` 5000 → 5 — bugfix (предыдущее значение было аномалией)
- `useScenePlayerAsHost` помечен `[Obsolete]` — старые сериализованные сцены работают
  как раньше, новый код не зависит от этого поля

---

## Связанные тикеты / тесты

- Тесты не запускал (по workflow пользователь запускает сам)
- Рекомендация: написать PlayMode-тест, который запускает host и проверяет:
  - ровно один `InventoryUI` в Hierarchy
  - ровно одна `ThirdPersonCamera`
  - `NetworkPlayer(Clone).IsOwner == true` для localClientId
  - scene-placed `PlayerSpawner.NetworkPlayer.enabled == false`
  (тест бы зафиксировал регрессию на будущее)
