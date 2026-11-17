# 18 — T-MB06: анализ расхождения осей и сверка с готовыми Unity-интеграциями

> **Дата:** 2026-11-17
> **Тип:** read-only анализ. Runtime-код в этом шаге **не менялся**, Play Mode не запускался.
> **Контекст:** после коммитов `899b148b`, `7b48161c`, `deaa106d` и текущих незакоммиченных правок
> (`SourceToTargetYawOffsetDeg = 0`, root = bind + `model_to_runtime`) визуальная ориентация
> персонажа всё ещё не сходится с направлением движения.
> **Связанные:** [17_T-MB06_RUNTIME_SIDECAR.md](17_T-MB06_RUNTIME_SIDECAR.md) · [T-MB06_RUNTIME_SIDECAR.md](../dev/T-MB06_RUNTIME_SIDECAR.md) · [01_RESEARCH.md](01_RESEARCH.md)

---

## 1. Короткий вывод

**Мы используем именно дефолтную human-модель NVIDIA — SOMA.** MotionBricks-SOMA-RP-v1 — официальный human-rig; вторая официальная модель — Unitree G1, робот, к нашему персонажу неприменим.

Проблема не в выборе SOMA. Проблема в том, что поверх официального runtime мы построили собственный слой, в котором **две независимые ошибки** дают одинаковый внешний симптом «персонажа крутит/ставит боком»:

1. **История MotionRecorder загрязняется внешним root игрока** — модель записывает позу уже после подмены root, не видит собственного поворота и накапливает компенсацию в костях.
2. **Ретаргет применяет мировую дельту в неправильном frame** — `delta · RestWorld` вместо rest-frame `RestWorld · bindWorld⁻¹ · frameWorld`.

Три последних коммита правили yaw-константу, высоту root, rest-template и twist, но **обе корневые ошибки остаются в текущем коде**.  
Поэтому менять 0/90/180° дальше бессмысленно: это перекладывает симптом между root, корпусом и руками.

## 2. SOMA — это NVIDIA default, а не альтернатива

| Что | Факт |
|---|---|
| Human model | **MotionBricks-SOMA-RP-v1**, 30 joints |
| Robot model | Unitree G1, 29 DoF |
| SOMA USD skeleton | 77 joints |
| Модель предсказывает | 30 именованных joints |
| Официальный Unity-пакет | отсутствует |

То есть «использовать не SOMA, а дефолт NVIDIA» технически невозможно: SOMA и есть дефолт для людей.

Другой вопрос — **на каком уровне использовать SOMA**:

- официальный runtime загружает 77-joint USD skeleton;
- модель реально управляет 30 joints;
- остальные 47 — пальцы, глаза, jaw и технические сегменты, которые не генерируются;
- наш `MotionBricksSomaRetarget` проносит все 77 и мапит 22.

Community bridge использует более простой интерфейс: **30-joint SOMA model space ↔ Unity Humanoid**. Для нашего игрового рига это могло бы резко уменьшить число преобразований.

## 3. Корневая причина №1: MotionRecorder получает подменённый root

Текущий `runtime_sidecar.py::step_character()`:

```python
character._sample_playback(dt)
...
_apply_external_root(character, app_ground, app_rotation)   # пишет root игрока в current_pose
...
character.player.record_pose(dt, character.current_pose)    # эта же поза уходит в историю
```

Официальная модель строит следующий context из последних **фактически сыгранных** поз. Если в `record_pose` попадает root, уже переписанный из Unity-игрока, модель получает:

- команду «повернись»,
- собственный сгенерированный поворот стёрт,
- в следующем чанке она снова пытается довернуть,
- остаток уходит в локальные повороты костей и накапливается.

Это не просто теоретическая претензия: текущий probe `verify_pose_stability.py` уже зафиксировал эффект на круговом вводе:

| Метрика (local delta к bind) | pin-before-record | record-own |
|---|---:|---:|
| spine | 45° | 25° |
| chest | 78° | 27° |
| thigh.L | 134° | 82° |
| arm.R | 178° | 78° |
| восстановление на простое | нет (arm.R 115°) | да |

Но **исправленный порядок не внесён в `runtime_sidecar.py`**: текущий файл по-прежнему вызывает `_apply_external_root()` до `record_pose()`. Числа `record-own` относятся к эксперименту, не к текущему runtime. Кроме того, в probe есть открытый дефект: после отпускания кнопок корпус продолжал складываться (`bodyLean 26 → 72°` за 1 s).

Правильный контракт: MotionRecorder должен получать **собственную позу модели**, до внешней подмены. Внешний root применяется только к копии позы для отправки в Unity, а не к `character.current_pose`, из которого потом строится context.

## 4. Корневая причина №2: неверный frame при переносе поворота кости

`MotionBricksSomaRetarget.Apply()`:

```csharp
delta = frameWorld * inverse(bindWorld);
animatedWorld = delta * node.RestWorld;       // текущая формула
```

Это корректно только если rest-поза target-рига совпадает с bind-позой SOMA по мировым ориентациям. У KL-рига и SOMA они разные. Для двух разных rest-поз правильный перенос:

```text
targetWorld = RestWorld(target) * bindWorld(source)⁻¹ * frameWorld(source)
```

Кватернионы не коммутируют: `delta * Rest` и `Rest * bind⁻¹ * frame` дают разные позы.
Ошибка проявляется как:

- «root вроде правильный, но корпус/руки повёрнуты не туда»;
- вывернутые плечи/предплечья;
- необходимость вручную клампить twist;
- различия между KL-моделью и `testing.fbx`.

Это подтверждает готовый G1 Unity-ретаргетер:

```csharp
target.rotation = targetRootRotation * reference * Quaternion.Inverse(zero) * current;
```

(`community/Unity6_motionbricks_test/.../G1HumanoidRetargeter.cs:279-281`)

Тот же вывод уже отмечен в текущем временном `_dump_bind_world.py`: при `rest != bind` формула `delta * RestWorld` геометрически неверна.

**Следствие:** `SourceToTargetYawOffsetDeg` не может исправить порядок умножения. 90/0/180° — это подгонка симптома.

## 5. Корневая причина №3: конвенции координат и владельцы root размножены

В коде одновременно существуют:

| Место | Преобразование |
|---|---|
| Sidecar `unity_to_app_position` | `(x,y,z) → (z,−x,y)` |
| Sidecar `unity_to_app_rotation` | basis `(0.5,0.5,0.5,0.5)` + отражение |
| C# `ToUnityPos/Rot` | `(−x,y,z)`, `(x,−y,−z,w)` |
| Community bridge | `z→−z`, `(−x,−y,z,w)` |
| Официальный app | `model_to_runtime = Rx(+90°)` |

Численная проверка чистого yaw-roundtrip через sidecar + `M⁻¹` + текущий C# mirror даёт identity для yaw 0/30/90/180/270. **То есть проблема не обязана быть одной константой 90°.** Но у нас нет одного зафиксированного контракта, а `verify_root_frame.py` использует ту же формулу, что и C#, поэтому проверяет внутреннюю согласованность, а не соответствие Unity-миру.

Дополнительно root принадлежит двум владельцам:

- `_apply_external_root` ставит позицию/yaw игрока;
- `model_pose()` вычитает app-rotation обратно;
- C# снова применяет root-дельту к Hips;
- при этом recorder получает уже подменённый root.

Для нашей игры правильнее:

**World XZ + yaw — только `CharacterController`/`NetworkPlayer`. Runtime отдаёт локальную позу относительно root и вертикальный bob.** Root генерации не должен возвращаться ни в историю, ни в Hips как мировая ориентация.

## 6. Почему три последних коммита не могли исправить картину

| Коммит | Что сделал | Почему недостаточно |
|---|---|---|
| `899b148b` | Выбор rest-template из body model | Правильно, но не меняет порядок delta |
| `7b48161c` | Исправил root height/history Z | К ориентации костей не относится |
| `deaa106d` | Yaw 90°, clamp twist, airborne fallback | Лечение симптомов |
| текущая незакоммиченная правка | Yaw 0°, root reference | Не меняет record-order и delta-frame |

Пока MotionRecorder видит чужой root и rest-frame перенос неверен, любые yaw-константы будут лишь менять, где именно вылезает скрутка.

## 7. Что есть в готовых Unity-решениях

| Репозиторий | Полезное | Ограничение |
|---|---|---|
| `motionbricks-unity-bridge` | **30-joint SOMA↔Unity**, `z→−z`, direct humanoid mapper, 30 fps playback, chained generation | Требует пустой Animator, direct bone writes, нет NetworkPlayer/equipment/FO |
| `Unity6_motionbricks_test` | **Правильная rest-frame delta-формула**, limb IK, G1 demo | G1 — робот, не SOMA human |
| `sa-inbetween` | CPU bake, `.saib` | Не runtime, не Unity state machine |

Их нельзя взять как production-компонент. Но **математику надо было переиспользовать**:

- bridge — 30-joint canonical wire/mirror;
- G1 retargeter — `RestWorld * bindWorld⁻¹ * frameWorld`;
- официальный runtime — SOMACharacter/recorder/state machine.

Причины, по которым bridge не подошёл напрямую (пустой Animator, ownership, body swap, equipment), объясняют необходимость **своего integration adapter**, но не оправдывают собственную математику координат без reference-тестов.

## 8. Рекомендуемый следующий этап

1. **Остановить** дальнейшие yaw/root-костыли.
2. Перевести runtime wire на **30-joint SOMA model layout** (77 оставить внутри официального runtime).
3. В Python отдать наружу **локальную позу**; recorder получает собственную позу модели **до** `_apply_external_root`.
4. В C# заменить формулу на `RestWorld * bindWorld⁻¹ * frameWorld`.
5. Зафиксировать один space contract (рекомендуется community bridge: `z→−z`, Unity↔SOMA; `M=Rx90` только на одной стороне).
6. Собрать reference A/B в отдельной dev-сцене: официальный engine + bridge mapper на стандартном Humanoid, проверить оси; затем повторить на KL-модели и `testing.fbx`.
7. Только после этого — интеграция в `NetworkPlayer` и замеры 1/2/4 клиентов.

## 9. Acceptance

- T-pose/idle: `up=+Y`, `forward=+Z`, `right=+X` после roundtrip.
- W → персонаж и лицо движутся в +Z; A/D без 90°-сдвига.
- Yaw 0/90/180/270 → тело и velocity совпадают.
- L/R стопа и рука не меняются местами.
- `delta_root` identity **и** независимый basis-roundtrip PASS.
- Pose stability на круговом вводе и восстановление после idle PASS.
- Один результат на KL Male/Female и `testing.fbx`.

---

**Изменено:** только этот документ. Runtime-код, три последних коммита и незакоммиченные правки не откатывались. Play Mode не запускался.
