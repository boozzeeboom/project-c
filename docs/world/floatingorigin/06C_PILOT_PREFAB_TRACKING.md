# T-FO06C — отслеживание пилотного префаба в Git

Дата: 2026-09-09. Предыдущий этап: T-FO06B, `8b6db842`.

## 1. Решение

Пилотный префаб floating-origin теперь отслеживается Git. Общее правило `*.prefab` **не снималось**: добавлено узкое исключение только для папки пилота.

Причина отказа от глобального снятия: правило `*.prefab` из блока «Content assets — tracked via Lore VCS, not git» действует на весь проект. Его удаление добавило бы в Git все префабы — корабли, NPC, зоны, тестовые объекты — то есть массовое изменение, которое не запрашивалось и конфликтовало бы с существующим Lore VCS.

## 2. Изменение `.gitignore`

Добавлено в конец файла, после правил `*.prefab` (строка 153) и `*.meta` (строка 182), иначе отрицания не сработали бы:

```gitignore
!Assets/_Project/Prefabs/FloatingOrigin.meta
!Assets/_Project/Prefabs/FloatingOrigin/**/*.prefab
!Assets/_Project/Prefabs/FloatingOrigin/**/*.prefab.meta
```

`.meta` включены сознательно: без них GUID префаба нестабилен при клонировании репозитория, и ссылки на ассет ломаются. Общее правило `*.meta` для остального проекта сохранено.

Сам `.gitignore` числится в собственном списке игнорирования (строка 137), но остаётся отслеживаемым файлом, поэтому правка коммитится нормально.

## 3. Проверка изоляции правила

`git check-ignore -v` после изменения:

| Путь | Результат |
|---|---|
| `Assets/_Project/Prefabs/FloatingOrigin/NetworkPlayer_GlobalPilot.prefab` | отслеживается (совпадает отрицание, строка 190) |
| `…/NetworkPlayer_GlobalPilot.prefab.meta` | отслеживается (строка 191) |
| `Assets/_Project/Prefabs/FloatingOrigin.meta` | отслеживается (строка 189) |
| `Assets/_Project/Prefabs/NetworkPlayer.prefab` | по-прежнему игнорируется (`*.prefab`) |
| `Assets/_Project/Scenes/BootstrapScene.unity` | по-прежнему игнорируется (`*.unity`) |

Canonical префаб игрока и сцены остаются вне Git, как и было.

## 4. Состояние пилотного префаба

Повторная read-only верификация после правки `.gitignore` — состояние не изменилось:

- контракт Spatial: **PASS**, features = RootNetworkObject, Adapter, Replicator, CoordinatesRequired, SpatialContent, Player, PlayerAttacker, PlayerTarget;
- 6 NetworkBehaviour, все пять флагов NetworkObject = false;
- нет stock writers, вложенных NetworkObject, body/joint/nav/2D и colliders вне root CC;
- canonical префаб остаётся legacy;
- 8 защищённых файлов побайтово равны снимку 06A;
- автогенерация выключена, реестр = 58 записей, пилот не зарегистрирован и не выбран как PlayerPrefab.

Unity: **No compile errors**.

## 5. Ограничения

- Правка касается только версионирования. Пилот не активирован, global mode/world shift выключены, runtime-проверок не было.
- Для остальных префабов и сцен источником правды остаётся Lore VCS; двойное ведение одного ассета в Git и Lore не вводилось.
- Если в пилотную папку попадут другие префабы, они также станут отслеживаемыми — это следствие шаблона по папке.

## 6. Состав коммита

`.gitignore`, пилотный префаб с его `.meta`, `.meta` пилотной папки, этот отчёт, уточнение §6 в `06B_PILOT_PLAYER_PREFAB.md`, roadmap и `Assets/_Project/Docs/ITERATIONS.md`.

Не включаются: TMP fallback, Temp-скрипты, `DefaultNetworkPrefabs.asset`, canonical префаб и сцены.
