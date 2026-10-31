# Ресерч: корабли без склейки + разрушаемость (Unity 6, Project C)

> Дата: 2026-10-30. Подготовлено для Project C (Stage 2.5, визуальный прототип, Unity 6000.5.2f1, URP 17.5, NGO 2.13, StandaloneWindows64). Без кода. Сопутствующий документ — `docs/dev/research_massive_unmerged_objects.md` (общий ресёрч по массовым объектам); здесь — узкий сценарий **транспорт/корабли без склейки с разрушаемостью**, «абсолютно не нагружаемой».

---

## 1. Краткое резюме

1. **Корабли простые (кусок корпуса + простые элементы) + «абсолютно не нагружаемая» разрушаемость — достижимо без DOTS**, но только в стиле **pre-fractured (Fortnite-стиль)**: заранее нарезанные куски, при ударе — swap визуала + активация физики на уже существующих (пуленных) объектах. Никакого runtime boolean/slicing на каждый удар.
2. **Совмещать PhysX-root корабля с DOTS-chunks не стоит.** Unity Physics (DOTS) и PhysX — два разных физических мира, у них нет общей сцены/контактов без ручной конвертации. Гибрид «PhysX корабль + DOTS обломки» создаёт дублирование состояния, проблемы с коллизиями между мирами и не даёт выигрыша, пока обломков мало. **Держать и корабль, и куски на PhysX (GameObject/Rigidbody).**
3. **«Абсолютно не нагружаемая» разрушаемость** = pre-fractured куски в пуле (не Instantiate), активация (enable) вместо создания, debris-lifetime (автоудаление мелких), LOD/impostors для дальних обломков, коллайдеры только на крупных кусках, sleep states, игнорирование коллизий между мелкими обломками.
4. **Сеть:** не делать NetworkObject на каждый кусок. Серверная истина — состояние корабля (битовая маска/список разрушенных кусков); клиенты локально спавнят визуальные куски по маске. Крупные обломки, влияющие на геймплей (блокировка прохода, урон), — сетевые, мелкие — локальные.
5. **Floating Origin:** простые объекты (без NetworkObject) едут с миром бесплатно под корнями `WorldScene_*`; рантайм-спавн вне корней — регистрация участником; кэши `Vector3` — `ApplyRebaseTranslation`; сейвы — кумулятив. Сетевые объекты (с NO) — полная регистрация §3.2 пайплайна (`09S_NEW_CONTENT_PIPELINE.md`).
6. **GPU Resident Drawer** даёт массовый рендер однотипных обломков (один draw call на меш+материал), но требует Forward+, `DOTS_INSTANCING_ON` в шейдерах, SRP Batcher, `Keep All` BRG variants. Кастомные шейдеры без `DOTS_INSTANCING_ON` — fallback без instancing.
7. **Реалистичный потолок без DOTS:** сотни одновременно активных кусков с физикой; визуально — десятки тысяч обломков через GPU RD. Полноценный boolean на 100k объектов — недостижим.

---

## 2. Контекст и ограничения

- **Стек:** Unity 6000.5.2f1, URP 17.5, NGO 2.13 (GameObject-based), StandaloneWindows64, New Input System, Cinemachine 3.1.7. **Без DOTS, без Entities, без Unity Physics (DOTS).**
- **Корабли:** простые — кусок корпуса + простые элементы (палуба, мачта, орудие). Не сложные многоуровневые конструкции.
- **Требование:** разрушаемость «абсолютно не нагружаемая» — не должна стоить CPU/GPU в нормальном режиме (без разрушения — ноль overhead; при разрушении — минимальный).
- **FO:** проект использует floating origin с ребейзом корней сцен (F8/авто/F9), кумулятивный offset, сетевой broadcast `FO06_REBASE_SHIFT`. Любые новые объекты с мировыми координатами подчиняются пайплайну `09S_NEW_CONTENT_PIPELINE.md`.
- **NGO:** сервер-авторитативный, visibility через `CheckObjectVisibility` + `NetworkShow/NetworkHide`. Встроенного AoI/grid нет.

---

## 3. Разрушаемость транспорта/кораблей в Unity

### 3.1. Подходы

| Подход | Суть | Плюсы | Минусы | Когда использовать |
|---|---|---|---|---|
| **Pre-fractured (Voronoi)** | Заранее нарезать меш на куски (редактор/загрузка); при ударе — показать куски + физика | Дёшевый runtime; красивый визуал; предсказуемо | Фиксированное разрушение; память на куски; коллайдеры на каждый кусок | **Корабли, здания, конструкции — рекомендуемый путь** |
| **Voxel** | Мир из вокселей; разрушение = удаление вокселей + регенерация меша/коллайдера чанка | Полностью разрушаемый мир; детерминизм | Свой пайплайн мешей/коллайдеров; дорого по памяти/CPU; визуал «кубический» | Только если нужна тотальная разрушаемость (Teardown-стиль) — большой отдельный проект |
| **Процедурный fracture (Voronoi runtime)** | Генерировать куски в момент разрушения | Гибко | 3–9 мс на объект; коллайдеры отдельно; непредсказуемо по времени | Демки; не для продакшена кораблей |
| **Runtime slicing/boolean** | Реальное разрезание меша по плоскости (OpenFracture, MeshCutting) | Любое разрезание | Дорого; сложные коллайдеры; артефакты; нестабильно | Специфические механики (мечи, лазеры) — не для корпуса корабля |
| **Compound + разборка** | Объект состоит из заранее подготовленных частей; при ударе — отсоединять части | Просто; предсказуемо; дёшево | Не «настоящее» разрушение | Мебель, техника, мелкие элементы корабля (орудия, мачты) |

### 3.2. Примеры игр

| Игра | Техника | Что взять для Project C |
|---|---|---|
| **Teardown** | Полностью воксельный мир; ray tracing; разрушение = удаление вокселей + перестройка чанка | Если нужна тотальная разрушаемость — только воксельный путь; иначе pre-fractured |
| **Fortnite** | Здания pre-fractured на куски; при уроне куски отваливаются | **Прецедент для кораблей:** pre-fractured подход реалистичен в Unity без DOTS |
| **BattleBit** | Voxel-ish разрушение зданий (чанки) | Чанковая модель для крупных конструкций |
| **Star Citizen** | Maelstrom — система разрушения кораблей (damage model + pre-fractured компоненты); корабли конвертируются из старой модели | Damage model по компонентам (орудия, двигатели, щиты) + визуальное разрушение корпуса; компоненты имеют отдельные пулы здоровья |
| **Sea of Thieves** | Корабль получает дыры от пушечных ядер; вода поступает; ремонт досками; корпус — набор сегментов с состоянием повреждения | Сегментированная модель повреждений: корпус делится на зоны, каждая зона имеет состояние (целая/повреждённая/дыра); сеть синхронизирует состояние зон, не куски |
| **World of Warships** | Модули корабля (орудия, двигатели, руль) имеют отдельное здоровье; визуальные повреждения (пожары, дым) + отваливающиеся части | Модульная система: корабль = набор модулей с HP; разрушение модуля → визуальный эффект + потеря функции |
| **Rust / Valheim** | Базы из строительных блоков; разрушение = удаление блока | Модульное строительство из готовых блоков; сетевой синк по блокам |

### 3.3. Без покупок — open-source инструменты

- **OpenFracture** (github.com/dgreenheck/OpenFracture) — runtime slicing + fracturing, поддерживает convex/non-convex меша. Ограничение: slicing только в runtime (не в редакторе).
- **unity-mesh-fracture** (github.com/sinanata) — Voronoi pre-bake на loading screen, runtime spawn кусков с физикой, dissolve через alpha-fade. Pre-cooked convex hulls (`Physics.BakeMesh`) — runtime `MeshCollider` assignment без cook.
- **Mesh-Cutting** (github.com/KristinLague) — простое разрезание меша по плоскости.
- **ProBuilder** (встроенный) — для ручной нарезки мешей в редакторе.
- **Blender** — Voronoi fracture (Cell Fracture addon) для подготовки кусков офлайн.

### 3.4. Рекомендация для кораблей Project C

**Pre-fractured (Fortnite-стиль) + compound-разборка для мелких элементов.**

- Корпус корабля заранее нарезается в редакторе (Blender Cell Fracture или ProBuilder) на 8–32 куска (не 100+ — иначе коллайдеры съедят бюджет).
- Каждый кусок — отдельный GameObject с MeshFilter + MeshRenderer + (опционально) MeshCollider (convex, pre-baked).
- При ударе: скрыть целый корпус, показать куски, применить силу к Rigidbody кусков.
- Мелкие элементы (орудия, мачты, ящики) — compound-разборка: заранее подготовленные части, отсоединение при ударе.
- **Не использовать runtime boolean/slicing** для корпуса — дорого и нестабильно.

---

## 4. PhysX vs DOTS Physics для кусков

### 4.1. PhysX (встроенный, GameObject-based)

- **Лимит: 65 536 коллайдеров на сцену** (историческое ограничение; в новых версиях снято, но производительность деградирует).
- **Rigidbody:** десятки тысяч активных rigidbody → CPU-bound, особенно при контактах. Статические коллайдеры (без Rigidbody) дёшевы, но всё равно занимают память и участвуют в broadphase.
- **MeshCollider:** convex — до 255 треугольников; non-convex не сталкиваются с non-convex. Compound colliders — предпочтительны.
- **CCD, фрикция, стекинг** — хорошо работают, но дорого.
- **Sleep:** PhysX автоматически усыпляет неподвижные rigidbody (sleep threshold). Это ключевая оптимизация для обломков.
- **Итог для кораблей:** PhysX потянет десятки-сотни активных кусков с физикой. Для кораблей (8–32 куска) — более чем достаточно.

### 4.2. Unity Physics (DOTS Physics, com.unity.physics)

- Детерминированный, Burst-компилируемый, ECS-native. Коллайдеры — `PhysicsCollider` (blob-ассеты, общие для сущностей → экономия памяти).
- **Масштаб:** 100k+ rigidbody в benchmark'ах (падающие кубы) — десятки FPS на десктопе; 10k — комфортно.
- **Ограничения:**
  - Менее стабильный стекинг (башни падают хуже, чем в PhysX).
  - Фрикция/отскок исторически имели баги.
  - Нет «спящего» состояния как в PhysX (все тела активны, хотя есть оптимизации для статичных).
  - Ограниченная поддержка сложных joint'ов, ragdoll'ов.
  - **Детерминизм требует фиксированного timestep и избегания операций >50 единиц от origin** — критично для floating origin (наш проект использует FO с ребейзом).
- **Havok Physics** (com.unity.physics.havok) — альтернатива, более стабильная, платная лицензия для продакшена.

### 4.3. Совместимость: можно ли держать PhysX root + DOTS chunks?

**Короткий ответ: технически можно, но не стоит.**

- Unity Physics (DOTS) и PhysX работают в **разных физических мирах**. Unity Physics симулирует entity в ECS-world, PhysX — GameObject'ы в UnityEngine.PhysicsScene. Они не разделяют broadphase, контакты между ними не генерируются автоматически.
- **Мосты:**
  - `PhysicsScene` (PhysX) и `PhysicsWorld` (Unity Physics) — отдельные. Чтобы DOTS-обломок столкнулся с PhysX-кораблём, нужна ручная конвертация (raycast из одного мира в другой, или дублирование коллайдеров).
  - `IConversionSystem` / `PhysicsBodyAuthoring` — конвертирует GameObject'ы в entity при bake, но это статическая конвертация, не runtime-мост.
  - `Unity.Physics.Hybrid` (устаревший пакет) — пытался связать GameObject и Entity, но deprecated.
- **Практические проблемы гибрида:**
  - Дублирование состояния: позиция обломка живёт и в PhysX-Rigidbody, и в Entity-Transform. Синхронизация — ручная, каждый кадр.
  - Floating origin: PhysX-ребейз сдвигает Rigidbody через `position += T`; DOTS-entity — через `LocalTransform`. Нужно сдвигать оба. У нас уже реализован PhysX-ребейз; добавление DOTS-мира удваивает работу.
  - Сеть: NGO работает с GameObjects; DOTS-entity не имеют NetworkObject. Связь DOTS↔сеть — через Netcode for Entities (отдельный пакет, миграция).
  - Отладка: два физических мира, два набора инструментов (Physics Debugger + DOTS Physics Debug).

**Вывод:** гибрид «PhysX корабль + DOTS обломки» не даёт выигрыша для кораблей (обломков мало, десятки, а не тысячи), но добавляет сложность. **Держать корабль и куски на PhysX.** DOTS имеет смысл только при переходе всего проекта на ECS (включая сеть) — что не входит в текущий scope.

### 4.4. Когда DOTS всё-таки нужен

- Если обломков **сотни тысяч** одновременно (не наш случай — кораблей мало, кусков 8–32).
- Если нужен **детерминизм** (сетевая физика с предсказанием) — но NGO 2.13 + PhysX уже даёт сервер-авторитативную физику.
- Если проект мигрирует на Netcode for Entities — тогда и физика должна быть DOTS.

### 4.5. Netcode for Entities vs NGO

- **NGO 2.13** — работает с GameObjects, NetworkObject, NetworkBehaviour. Нативная интеграция с PhysX (Rigidbody синхронизируется через NetworkTransform/NetworkRigidbody).
- **Netcode for Entities** — работает с Entities, требует полной миграции геймплея на ECS. Не совместим с NGO без моста.
- **Наш выбор:** остаться на NGO 2.13 + PhysX. Не мигрировать.

---

## 5. Compound colliders для кораблей

### 5.1. Как строить

- **Иерархия:** один GameObject с Rigidbody (root) + дочерние GameObject'ы с primitive colliders (Box, Sphere, Capsule). Rigidbody только на root.
- **Принцип:** аппроксимировать форму корабля минимальным набором примитивов. Корпус — 2–4 Box Collider'а; мачта — Capsule; орудие — Box + Capsule.
- **Повороты:** дочерние коллайдеры можно вращать относительно root (rotated boxes), но не использовать shear-трансформы (non-uniform scale + rotation ломает примитивы).
- **MeshCollider (convex) на root** — альтернатива, но только если примитивы не аппроксимируют форму (например, сложная геометрия корпуса). Convex hull заполняет вогнутости → неточные коллизии.

### 5.2. Производительность

- **Sphere < Capsule < Box < Convex MeshCollider < Compound (N примитивов).**
- Compound collider с 4–8 примитивами — дешевле одного convex MeshCollider на 1000-полигональный меш.
- **Правило:** использовать как можно меньше примитивов; каждый лишний коллайдер — лишняя пара проверок в broadphase.
- **Sleep:** после остановки корабля PhysX усыпит Rigidbody → коллайдеры не участвуют в broadphase (но всё ещё занимают память).

### 5.3. Когда использовать MeshCollider

- **Статическая геометрия** (острова, причалы, здания) — non-convex MeshCollider (concave), без Rigidbody. Дёшево для статики.
- **Сложные динамические объекты** (корабль с нестандартной формой) — convex MeshCollider с лимитом 255 треугольников, либо V-HACD (approximate convex decomposition) для точной аппроксимации.
- **Разрушаемые куски** — convex MeshCollider на каждый кусок (pre-baked через `Physics.BakeMesh`), либо примитивы (Box/Capsule) для простых кусков.
- **Не использовать MeshCollider (non-convex) на динамических объектах** — запрещено PhysX'ом (ошибка runtime).

### 5.4. Для разрушаемых кусков

- **Крупные куски (корпус):** convex MeshCollider (pre-baked) или 2–3 примитива.
- **Мелкие куски (щепки, осколки):** без коллайдеров (визуально только) или триггеры (OverlapSphere для сбора лута).
- **Ограничение:** не более ~100 активных кусков с коллайдерами одновременно (бюджет PhysX).

---

## 6. GPU instancing / GPU Resident Drawer

### 6.1. Требования

- **Forward+ rendering path.**
- **Графический API с compute shaders** (не OpenGL ES).
- **SRP Batcher включён.**
- **Шейдеры с поддержкой `DOTS_INSTANCING_ON`** — без этого объекты рендерятся без instancing.
- **`BatchRendererGroup Variants = Keep All`** в Project Settings → Graphics → Shader Stripping.
- **GPU Resident Drawer = Instanced Drawing** в URP Asset.

### 6.2. Ограничения

- Работает только для **MeshRenderer** (не SkinnedMeshRenderer с проблемными шейдерами, не SpriteRenderer, не VFX).
- **Build time растёт** (компиляция всех BRG-вариантов).
- **Разные меши/материалы → разные батчи.** 1000 уникальных мешей = 1000 батчей. Для кораблей/обломков нужна стандартизация мешей (несколько вариантов корпуса, а не уникальные).
- **Объекты, меняющиеся каждый кадр, обновляются** (есть overhead).
- **Кастомные шейдеры** без `DOTS_INSTANCING_ON` — fallback без instancing (один draw call на объект). Стандартные URP шейдеры (Lit, Unlit) поддерживают.
- **LOD не поддерживается** с GPU Resident Drawer (ограничение из документации).

### 6.3. Применимость к кораблям/обломкам

- **Обломки одного типа** (одинаковый меш+материал) — один draw call на тип. 1000 обломков корпуса → 1 draw call.
- **Разные типы обломков** (дерево, металл, камень) — 3–5 draw calls (по типу материала).
- **Корабли вдалеке** — impostors/billboards (не GPU RD, а отдельные спрайты).
- **Практический вывод:** GPU RD даёт массовый рендер однотипных обломков, но требует стандартизации мешей/материалов. Для уникальных кораблей (каждый корабль уникален) — не поможет; нужна LOD-цепочка + impostors.

---

## 7. «Абсолютно не нагружаемая» разрушаемость

### 7.1. Pre-fractured + pooling + activation

**Ключевой принцип: не создавать объекты в момент разрушения — активировать уже существующие.**

- **Пул кусков:** при загрузке корабля (или при старте сцены) создать все куски корпуса (8–32 шт.) как дочерние GameObject'ы, **выключенные** (inactive). При разрушении — включить (SetActive(true)), применить силу.
- **Не Instantiate/Destroy** — это дорого (аллокация, инициализация компонентов, регистрация в PhysX). Activation/deactivation — дёшево.
- **NGO Object Pooling:** для сетевых кусков использовать `INetworkPrefabInstanceHandler` (пул NetworkObject). Избегать Instantiate/Destroy.
- **Ограничение пула:** не более N одновременно активных кусков на корабль (например, 32). При повторном разрушении (если корабль уже разрушен) — не активировать повторно (флаг `isDestroyed`).

### 7.2. Debris lifetime (автоудаление мелких)

- **Мелкие обломки (щепки, осколки) живут 3–5 секунд**, затем исчезают (fade-out + deactivate).
- **Крупные куски (корпус) остаются** (или удаляются через 30–60 секунд).
- **MaxDynamicChunkCount:** ограничить общее количество активных обломков в сцене (например, 200). При превышении — удалять самые старые (FIFO).
- **PhysX sleep:** после остановки обломка PhysX усыпит Rigidbody → ноль CPU-стоимости. Не удалять уснувшие (они не стоят CPU), но удалять визуально дальние.

### 7.3. LOD для кусков

- **LOD Group на каждом куске:** LOD0 (высокополигональный, близко), LOD1 (средний), LOD2 (низкополигональный), LOD3 (impostor/billboard).
- **Culling:** дальние обломки не рендерятся (frustum culling + occlusion culling).
- **GPU RD + LOD:** GPU Resident Drawer не поддерживает LOD (ограничение). Для обломков с LOD — использовать обычный рендеринг (не GPU RD) или ручной LOD через скрипт.

### 7.4. Impostors для дальних обломков

- **Дальние обломки ( >50 м)** заменяются на billboards/sprites (один draw call на все impostors через SpriteRenderer или custom shader).
- **Переключение:** при приближении игрока — заменить impostor на 3D-меш.
- **Экономия:** 1000 дальних обломков → 1 draw call (impostor atlas).

### 7.5. Culling обломков

- **Frustum culling:** стандартный Unity (автоматически).
- **Occlusion culling:** baked occlusion для статической геометрии; динамические обломки — через `OcclusionArea` или ручной culling.
- **Distance culling:** обломки дальше R (например, 200 м) деактивируются (SetActive(false)) и возвращаются в пул.
- **Interest management (MMO):** сервер не шлёт обломки дальним игрокам (см. §8).

### 7.6. Коллизии обломков

- **Мелкие обломки не сталкиваются друг с другом** (Physics.IgnoreCollision между всеми мелкими). Они сталкиваются только с крупными объектами (корабли, земля, стены).
- **Layer matrix:** настроить `Physics.IgnoreLayerCollision` для слоёв (Debris ↔ Debris = ignore).
- **Convex MeshCollider с лимитом 255 три** для крупных кусков; мелкие — без коллайдеров.

---

## 8. MMO/сетевой аспект

### 8.1. Синхронизация разрушения

**Серверная истина — состояние корабля, а не куски.**

- Корабль имеет состояние: `int damageMask` (битовая маска разрушенных кусков) или `List<int> destroyedChunks`.
- При ударе по кораблю сервер обновляет `damageMask` и шлёт `Rpc` всем клиентам (или только заинтересованным).
- Клиенты по `damageMask` активируют локальные куски (визуально).
- **Куски не являются NetworkObject'ами** (по умолчанию). Это локальные визуальные объекты.

### 8.2. Кто видит какие куски

- **Крупные куски (корпус), влияющие на геймплей** (блокировка прохода, урон при столкновении) — NetworkObject, сервер-авторитативные. Их позиции синхронизируются.
- **Мелкие обломки (щепки, осколки)** — локальные, не сетевые. Каждый клиент генерирует свои мелкие обломки при разрушении (детерминированно по seed или просто локально).
- **Визуальные эффекты (дым, огонь, искры)** — локальные (Particle System/VFX Graph), не сетевые.

### 8.3. NetworkObject per chunk vs per ship

| Подход | Плюсы | Минусы | Рекомендация |
|---|---|---|---|
| **NO per chunk** | Точная синхронизация позиций кусков | Много NetworkObject'ов; трафик; серверная нагрузка; сложность | Только для крупных кусков (8–32 на корабль), не для мелких |
| **NO per ship** | Один NetworkObject; состояние — маска | Клиенты сами спавнят куски (может быть рассинхрон визуала) | **Рекомендуемый подход** для большинства случаев |
| **Гибрид** | Крупные куски — NO; мелкие — локальные | Сложность, но оптимально | Лучший баланс |

### 8.4. Interest management для обломков

- **NGO 2.13** даёт только `CheckObjectVisibility(clientId)` callback + `NetworkShow/NetworkHide`. Нет встроенного AoI/grid.
- **Наивный подход** (callback на каждый объект каждый тик) убьёт сервер при 1000+ объектов.
- **Рекомендуемая система:**
  1. **Пространственный хеш-индекс** (uniform grid, ячейки 64–256 м) на сервере. Обломки регистрируются в ячейках.
  2. **Батчевое обновление видимости:** раз в тик (или раз в N тиков) сервер пересчитывает, какие обломки видны каждому клиенту, и шлёт `NetworkShow/NetworkHide` батчами.
  3. **Радиус интереса:** обломки дальше R (например, 200 м) от игрока не синхронизируются.
  4. **Приоритет:** крупные куски (корпус) — высокий приоритет; мелкие — низкий (или вообще не сетевые).
- **Delta-синк:** не слать полное состояние каждый тик; слать только изменения (новые разрушенные куски, новые обломки).

### 8.5. Object pooling для NetworkObject

- Использовать `INetworkPrefabInstanceHandler` + пул NetworkObject'ов (NGO Object Pooling).
- Избегать `Instantiate`/`Destroy` для NetworkObject'ов (дорого, фрагментирует память).
- Пул создаётся при старте сцены; NetworkObject'ы переиспользуются.

### 8.6. Практические ограничения NGO

- **NGO 2.13 не работает с Entities.** Если мы хотим DOTS-физику/рендер, придётся мигрировать на Netcode for Entities.
- **Scene-placed NetworkObject'ы** (с `InScenePlacedSourceGlobalObjectIdHash == 0`) требуют `ScenePlacedObjectSpawner` — иначе их [Rpc] падают с NRE. У нас это уже реализовано.
- **NetworkPrefabsList не присвоен** → динамический спавн сломан (отдельный тикет). Для пула NetworkObject'ов нужно либо присвоить список, либо использовать кастомный handler.

---

## 9. Floating Origin для обломков

### 9.1. Контракт (две подсистемы)

| # | Подсистема | Что проверяет | Правило «под рут» |
|---|---|---|---|
| A | Сдвиг мира (`GlobalMotionControlledRebaseSlice`, F8/авто/F9) | Едут ли объекты вместе с миром | ✅ Достаточно: дети корней `WorldScene_*` едут бесплатно |
| B | Допуск при старте (`GlobalMotionPilotRuntime`) | Нет ли неизвестных системе NetworkObject | ❌ Недостаточно: каждый scene-placed NO обязан иметь маркер + запись в каталоге + digest |

### 9.2. Простые объекты (без NetworkObject) — правила §3.1 пайплайна

- 🟢 **Под корень `WorldScene_*`** — едут бесплатно (якоря парома лежат сиблингами под `WorldRoot_0_0` именно поэтому).
- 🟡 **Скрипт хранит мировые `Vector3` между кадрами** — `ApplyRebaseTranslation` + вызов в shift-блоке слайса (оба пути: success и rollback).
- 🔴 **Рантайм-спавн вне корней сцен** — регистрация участником или parent под участника.
- 💾 **Сейвы мировых координат** — хранить + применять кумулятив при загрузке.
- **Проверка:** F8 → `runtimeRebase.Completed` + объект на месте + 0 errors; F9.

### 9.3. Сетевые объекты (с NO) — полная регистрация §3.2

- Положить объект под корень своей сцены.
- Проверить физику (Rigidbody для `ShipOrRigidbodyRoot`, отсутствие для `SceneOwnedNetworkGameplay`).
- Запечь маркер `GlobalSceneSourceMarker` (`sourceId` через `GlobalObjectId.GetGlobalObjectIdSlow`).
- Добавить observation в `GlobalMotionPilotSceneCatalog`.
- Добавить entry (treatment, ownership, spatial, poseKind, parentSourceId, reviewNote).
- Обновить хэши соседей + digest в `GlobalMotionPilotProfile.asset`.
- Проверить `TryCompile=True` + `TryMatchDigest=True`.

### 9.4. Применение к обломкам

**Большинство обломков — простые объекты (без NO):**

- Если обломки спавнятся **под корнем сцены** (как дочерние объекты корабля) — едут бесплатно.
- Если обломки спавнятся **рантайм вне корней** (например, при разрушении корабля в полёте) — **регистрация участником** или parent под участника (корабль как участник).
- **Кэши `Vector3`** в скриптах обломков (например, `initialPosition`, `targetPosition`) — `ApplyRebaseTranslation` в shift-блоке.
- **Сейвы координат обломков** (если сохраняются) — кумулятив при загрузке.

**Крупные куски с NO (если используются):**

- Полная регистрация §3.2 (маркеры, каталог, digest).
- При ребейзе — сдвиг через `NetworkTransform.Teleport` (`NetworkPublished`), как для кораблей.

### 9.5. Проверка FO для обломков

- F8 (ребейз) → обломки на месте + 0 errors.
- F9 (откат) → возврат.
- Авто-ребейз (отлёт >256 м) → обломки едут с миром.
- Второй клиент → broadcast `FO06_REBASE_SHIFT` → обломки сдвигаются.

---

## 10. Сравнение подходов

| Аспект | Pre-fractured (Fortnite) | Voxel (Teardown) | Runtime fracture (OpenFracture) | Compound + разборка |
|---|---|---|---|---|
| **Runtime стоимость разрушения** | Низкая (activation) | Средняя (перестройка меша чанка) | Высокая (мс на операцию) | Низкая (отсоединение частей) |
| **Визуальное качество** | Хорошее (заранее подготовленные куски) | Отличное (произвольное разрушение) | Хорошее (любое разрезание) | Среднее (фиксированные части) |
| **Гибкость** | Низкая (фиксированное разрушение) | Высокая (любое место) | Высокая (любое место) | Низкая (фиксированные части) |
| **Память** | Высокая (все куски в пуле) | Средняя (чанки) | Низкая (генерация на лету) | Низкая (только части) |
| **Коллайдеры** | На каждый кусок (pre-baked) | На чанк (регенерация) | На каждый кусок (cook в runtime) | На каждую часть |
| **Сетевая синхронизация** | Маска разрушенных кусков | Diff чанков | Сложно (произвольная геометрия) | Маска отсоединённых частей |
| **Сложность реализации** | Средняя (подготовка ассетов) | Очень высокая (свой движок) | Средняя (библиотека) | Низкая |
| **Подходит для кораблей Project C** | **Да (рекомендуется)** | Нет (слишком сложно) | Нет (дорого) | Да (для мелких элементов) |

---

## 11. Что реально возможно в Unity 6 с нашим стеком (точечные меры)

### 11.1. Разрушаемость кораблей (pre-fractured)

1. **Подготовка ассетов:** нарезать корпус в Blender (Cell Fracture) на 8–32 куска; экспорт в FBX; импорт в Unity.
2. **Пул:** при загрузке корабля создать все куски (inactive), parent под корабль (или под корень сцены).
3. **Активация:** при ударе — SetActive(true) на кусках, скрыть целый корпус, применить силу (AddExplosionForce или AddForce).
4. **Debris lifetime:** мелкие куски — 3–5 сек, затем fade + deactivate.
5. **Коллайдеры:** convex MeshCollider (pre-baked) на крупных кусках; мелкие — без коллайдеров.
6. **Sleep:** PhysX автоматически усыпит неподвижные куски.
7. **LOD:** LOD Group на каждом куске; impostors для дальних.

### 11.2. Физика

1. **Compound colliders** на корабле (корпус — 2–4 Box, мачта — Capsule, орудие — Box+Capsule).
2. **Не использовать MeshCollider (non-convex) на динамических объектах.**
3. **Convex MeshCollider с лимитом 255 три** для сложных кусков (pre-baked).
4. **Layer matrix:** Debris ↔ Debris = ignore (мелкие не сталкиваются друг с другом).
5. **Fixed timestep** и уменьшение solver iterations где допустимо.

### 11.3. Рендер

1. **GPU Resident Drawer** (Forward+, SRP Batcher, Keep All, DOTS_INSTANCING_ON) для однотипных обломков.
2. **Атласы материалов** — один материал на класс обломков (дерево, металл, камень).
3. **LOD-группы + impostors** для дальних обломков.
4. **Occlusion culling** (baked + dynamic).
5. **Статический батчинг отключить** при включённом GPU RD (конфликт).

### 11.4. Сеть

1. **Серверная истина — damageMask** (битовая маска разрушенных кусков).
2. **Rpc на разрушение:** сервер шлёт маску; клиенты активируют куски локально.
3. **Крупные куски (корпус) — NetworkObject** (если влияют на геймплей); мелкие — локальные.
4. **Object pooling** для NetworkObject (NGO Object Pooling).
5. **Interest management:** пространственный хеш-индекс + батчевые NetworkShow/NetworkHide.
6. **Delta-синк:** только изменения, не полное состояние каждый тик.

### 11.5. Floating Origin

1. **Простые обломки (без NO)** — под корни сцен или регистрация участником.
2. **Кэши `Vector3`** — `ApplyRebaseTranslation`.
3. **Сейвы** — кумулятив.
4. **Сетевые куски (с NO)** — полная регистрация §3.2 + NetworkTransform.Teleport при ребейзе.
5. **Проверка:** F8/F9 + авто-ребейз + второй клиент.

---

## 12. Риски и что НЕ делать

### 12.1. Риски

1. **Память:** пул из 32 кусков × 10 кораблей = 320 GameObject'ов с MeshRenderer/MeshCollider. При 100 кораблях — 3200. Нужен стриминг (активны только ближние корабли).
2. **Коллайдеры:** 320 convex MeshCollider'ов — в пределах 65k cap, но broadphase деградирует при тысячах. Нужен distance culling.
3. **Сеть:** если каждый кусок — NetworkObject, трафик взорвётся. Использовать маску + локальную активацию.
4. **FO:** обломки, спавненные рантайм вне корней сцен, не поедут с миром → визуальный дрейф. Нужна регистрация.
5. **GPU RD:** без `DOTS_INSTANCING_ON` в шейдерах — fallback без instancing → тысячи draw calls.

### 12.2. Что НЕ делать

- **Не использовать runtime boolean/slicing** для корпуса корабля — дорого и нестабильно.
- **Не смешивать PhysX и DOTS** для кораблей/обломков — дублирование состояния, проблемы с коллизиями.
- **Не делать каждый кусок NetworkObject'ом** — убьёт сеть и сервер.
- **Не держать 100k активных GameObject'ов** с Update — убьёт CPU.
- **Не переходить на DOTS «частично»** без плана миграции сети (NGO→Netcode for Entities).
- **Не использовать MeshCollider (non-convex) на динамических объектах** — запрещено PhysX'ом.
- **Не забывать про FO** — обломки рантайм-спавна должны ехать с миром.

---

## 13. Источники

**Destruction (Unity):**
- OpenFracture — https://github.com/dgreenheck/OpenFracture
- unity-mesh-fracture — https://github.com/sinanata/unity-mesh-fracture
- Mesh-Cutting — https://github.com/KristinLague/Mesh-Cutting
- Game Developer: Teardown voxel destruction — https://www.gamedeveloper.com/design/combining-bombastic-heists-with-a-fully-destructible-voxel-world-in-i-teardown-i-
- Unity Discussions: recreate Teardown voxel physics — https://discussions.unity.com/t/want-to-recreate-teardown-destructable-voxel-physics/828648
- Springer: Real-time fracturing in video games — https://link.springer.com/article/10.1007/s11042-022-13049-x
- 80.lv: Real-Time Procedural Mesh Fracture Demo — https://80.lv/articles/unity-destruction-prototype-with-fully-procedural-runtime-fracturing

**Physics (PhysX vs DOTS):**
- Unity Manual: Built-in 3D physics (PhysX) — https://docs.unity3d.com/6000.3/Documentation/Manual/PhysicsOverview.html
- Unity Physics (DOTS) manual — https://docs.unity3d.com/Packages/com.unity.physics@1.0/manual/index.html
- Unity Discussions: PhysX future? — https://discussions.unity.com/t/physx-future/1709078
- Giannis Akritidis: Why Unity ECS Physics Isn't Ready — https://giannisakritidis.com/blog/ECS-Physics-Bug/
- Unity Discussions: ECS Physics vs PhysX test — https://discussions.unity.com/t/ecs-physics-vs-physx-test/948446

**GPU Resident Drawer / DOTS Instancing:**
- Unity Manual: Enable GPU Resident Drawer in URP — https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html
- Unity Manual: Support DOTS Instancing in custom shader — https://docs.unity.com/en-us/engine/6000.7/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/batch-renderer-group/writing-shaders/dots-instancing-shaders-support
- gamedevllm: GPU Resident Drawer deep dive — https://gamedevllm.com/en/unity-6-gpu-resident-drawer-deep-dive-en

**Collider performance:**
- Unity Manual: Collider types and performance — https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/physics-optimization/cpu/collider-types
- Unity Manual: Introduction to Mesh colliders — https://docs.unity3d.com/6000.5/Documentation/Manual/mesh-colliders-introduction.html
- Unity Manual: Compound colliders — https://docs.unity.cn/Packages/com.unity.physics%401.0/manual/concepts-compounds.html

**MMO / networking:**
- NGO manual: Object visibility — https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.5/manual/basics/object-visibility.html
- NGO manual: Object pooling — https://mp-docs.dl.it.unity3d.com/netcode/2.0.0/advanced-topics/object-pooling/index.html
- Photon Fusion: Interest Management — https://doc.photonengine.com/fusion/v2/manual/advanced/interest-management
- Mirror: Interest Management — https://mirror-networking.gitbook.io/docs/manual/interest-management

**Floating Origin:**
- Project docs: `docs/world/floatingorigin/README.md`, `09S_NEW_CONTENT_PIPELINE.md`
- Gist: Floating origin in Unity — https://gist.github.com/brihernandez/9ebbaf35070181fa1ee56f9e702cc7a5
- Wikipedia: Floating origin — https://en.wikipedia.org/wiki/Floating_origin

**Примеры игр:**
- Star Citizen: Maelstrom destruction system — https://starcitizen.tools/Maelstrom_(destruction_system)
- Sea of Thieves: ship damage (Rare Gamer preview) — https://www.raregamer.co.uk/setting-sail-with-sea-of-thieves-at-rare-hq
- Fortnite: pre-fractured buildings (Game Developer) — https://salivity.github.io/game-development/article/how-developers-make-destructible-environments-in-games

---

*Отчёт подготовлен как исследовательский документ. Реализация любого из подходов — отдельная задача с дизайн-нотой и одобрением. Сопутствующий документ — `docs/dev/research_massive_unmerged_objects.md`.*
