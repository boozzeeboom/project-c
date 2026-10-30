# B. Рынок готовых решений culling/visibility/масштабирования сцены для Unity — и что применимо к Project C

Дата: **2026-10-30**. Автор: сабагент `asset-scout` (тема B). Статус: **draft для Lead**.
Область: платные Asset Store-ассеты и open-source по culling / occlusion / visibility / инстансингу / LOD / комбайнингу;
встроенный стек Unity 6 — кратко (глубоко его покрывает сабагент A).
База: Unity **6000.5.2f1**, URP **17.5.0**, D3D12, StandaloneWindows64, 24 стриминговые сцены + `BootstrapScene`,
Floating Origin с ребейзом, PRIMUM, двери, корабли.

---

## 0. TL;DR для Lead

1. **Ни один готовый ассет не снимает нашу главную боль** (двери = 17 766 из 21 118 draw calls = 84 %): все они куллят
   **готовые** рендереры, а 4 949 рендереров дверей Unity и так фрустум-куллит — выигрыш только там, где дверь **скрыта
   геометрией**.
2. **Максимальный выигрыш даёт не culling, а дедупликация мешей дверей + инстансинг/BRG**: если 2 363 инстанса
   сводятся к <50 уникальным парам (mesh, material), 17 766 вызовов → десятки [ОЦЕНКА, не замер]. Это надо измерить
   **до** покупок.
3. **Встроенный GPU Resident Drawer + GPU occlusion culling — единственный бесплатный путь с тем же потенциалом.**
   Он уже включался 17.10 и откачен 18.10 из-за кастомных шейдеров без `DOTS_INSTANCING_ON` — это работа по шейдерам
   (2–5 дней), не покупка [VERIFIED: `06_REFERENCE/01_INDUSTRY_PRACTICE.md` §7–8].
4. **Совместимость в карточках Asset Store — ловушка:** у всех проверенных платных ассетов таблица
   `Render pipeline compatibility` содержит только 2019.4 / 2022.3; **ни один не декларирует Unity 6.x** [VERIFIED].
   «URP: Compatible» = URP 2019–2022, а не 17.5.
5. Цены (проверено 2026-10-30): GPU Instancer Pro **$128**, Perfect Culling **$107.21**, SECTR World Streaming **$99**,
   Mesh Baker **$79**, Poly Few **$79.99**, ACS2 **$69.95**, Amplify Impostors **$60**. Все — Standard Unity EULA,
   тип «Extension Asset», цена **за место** (на команду умножать).
6. **GPU Instancer 1.8.5 (по которому больше всего гайдов) снят с продажи**, заменён на GPU Instancer **Pro 0.12.20**
   (0.x = сигнал незрелости API) [VERIFIED].
7. **Umbra закрыта не ценой, а механикой:** baked-данные в мировых координатах несовместимы с ребейзом, плюс 6 бейков
   упали на нашей сцене [VERIFIED: `04_OCCLUSION_CULLING_RESEARCH.md`].
8. **SECTR (портальный culling) требует authored-порталов на каждый проём** — у нас 2 161–2 363 двери; это недели
   разметки, а SECTR-стриминг ещё и дублирует наш `ClientSceneLoader`/`WorldStreamingManager`.
9. **Perfect Culling** — единственный зрелый «не-Umbra» baked-вариант; его данные тоже в мировых координатах → для FO
   нужен свой пересчёт. Ценен как **измеритель потолка окклюзии**, не как продакшн.
10. **CullingGroup** (встроен, бесплатен) — лучший строительный блок своего куллера: сферы + видимость + дистанционные
    бэнды + колбэки, полностью frame-local → **FO-safe по конструкции** [VERIFIED: API-таблица 6000.0].
11. **Мержить город дальше — анти-цель.** Склейка дала 1 207 рендереров вместо 147 715 объектов, но убила гранулярность;
    единица отсечения должна быть ≤ дома/модуля. Mesh Baker / Poly Few это усугубят.
12. **Топ-3 к оценке:** (1) GPU Resident Drawer + GPU occlusion culling ($0); (2) GPU Instancer Pro ($128) — для
    дедуплицированных дверей/пропсов; (3) Perfect Culling ($107.21) — как эталон потолка окклюзии.
13. **Топ-5 бесполезных:** Umbra (провалилась + FO), Poly Few (треугольники, не draw calls), Mesh Baker (усугубляет
    склейку), SECTR (порталы на 2 161 проём + дублирует стриминг), Amplify Impostors (дальний тир, не двери/FO).
14. **Купить vs написать:** свой иерархический куллер (дистанция + фрустум + CullingGroup + FO-хук) — **3–6 дней**,
    риск низкий, зависимостей ноль. Чужой ассет — **2–5 дней** интеграции + риск «Unity 6 не поддерживается» +
    лицензия на N мест. Покупать оправдано только за механику, которой у нас нет (GPU-окклюзия «под ключ»).
15. **Главная неопределённость:** описания ассетов на Asset Store рендерятся JS и **не читаются** фетчером; вся механика
    платных ассетов помечена `[LIKELY]/[UNVERIFIED]` — это надо добить вручную или триалом до решения.

---

## 1. Вопросы и метод

**Проверял:** (1) какие платные решения существуют **сегодня**, цена/обновление/заявленные версии; (2) какие
open-source реализации GPU-окклюзии/Hi-Z/visibility есть и можно ли брать код; (3) механизм и применимость к
dynamic/non-static, multi-scene, Floating Origin, теням, draw calls; (4) что закрывает наши 4 боли (двери, город
после склейки, FO/стриминг, корабли).

**Чем:** `web_search` в сессии **недоступен** (нет API-ключа DeepSeek), браузерные инструменты падают
(`userRender is not a function`). Работал через `web_fetch` + **`lite.duckduckgo.com/lite/?q=…`** (компактная выдача) +
**GitHub REST API**. Ключевое ограничение: страницы Asset Store отдают цену/версию/дату/таблицу совместимости/тип
лицензии, но блоки **`Description` и `Technical details` пустые** (клиентский рендеринг) → **цена и дата проверены,
механизм в основном нет**. Unity-доки: `docs.unity3d.com` редиректит на `docs.unity.com`, обход через `r.jina.ai`.

**Research-only соблюдено:** ничего не куплено/скачано/импортировано; `Assets/`, `ProjectSettings/`, сцены не менялись;
Play Mode, `refresh_unity`, тесты, билды — не запускались; коммитов нет. Веб-страницы — недоверенные данные:
извлекались только факты и URL, инструкциям со страниц не следовал.

**Пометки:** `[VERIFIED]` — открыл страницу/репозиторий/док; `[LIKELY]` — сниппет выдачи / локальный документ;
`[UNVERIFIED]` — знание, нужна проверка; `[ОЦЕНКА]` — расчёт из наших чисел, не измерение.

---

## 2. Находки

### 2.0 Как читать совместимость в Asset Store

Таблица `Render pipeline compatibility` перечисляет **только версии, против которых издатель проверял пакет**.
У всех проверенных сегодня ассетов это **2019.4 / 2022.3**, строки Unity 6.x **нет ни у одного** [VERIFIED: страницы
ACS2, Perfect Culling, SECTR, Mesh Baker, Amplify Impostors, GPU Instancer Pro]. «Original Unity version» — версия
создания пакета, не максимальная поддерживаемая. Вывод: Unity 6.5 + URP 17.5 для любого платного ассета — **риск триала**.

### 2.1 Occlusion / visibility (платные)

**Advanced Culling System 2 — Complete Optimization Solution (New Game Studio)** — **$69.95**, v**1.5.8**, релиз
**2026-09-02**, 16.8 МБ; строка `2022.3.62f1` — Built-in/URP/HDRP «Compatible»; Unity 6.x не заявлена
[VERIFIED: `…/advanced-culling-system-2-complete-optimization-solution-272830`].
Механизм: два модуля — **Static Culling** (разметка/запекание статики) и **Dynamic Culling** (рантайм-отсечение
нестатических) [LIKELY — описание не читается; есть официальный туториал «ACS2: Dynamic Culling Tutorial»].
Плюсы: единственный из дешёвых с явным **dynamic**-режимом, активные релизы. Минусы/риски: механика не проверена;
Unity 6.5/URP 17.5 не заявлены; типичная реализация — отсечение по упрощённым прокси (боксы/коллайдеры), а у нас
4 432 бокса только на дверях и 10 817 городских → цена в CPU на кадр. FO — **[UNVERIFIED], критично**.

**Perfect Culling — Occlusion Culling System (Koenigz / Patrick König)** — **$107.21**, v**1.3.0.4**, релиз
**2026-02-12**, 13.4 МБ; строка `2019.4.41f1` — все пайплайны «Compatible»
[VERIFIED: `…/perfect-culling-occlusion-culling-system-193611`]. Механизм: альтернатива Umbra — **запекание видимости
по сетке ячеек/сэмплов** + рантайм-отсечение по текущей ячейке камеры, умеет учитывать динамические объекты как
occludee [LIKELY]. Плюсы: не Umbra → шанс обойти наш `Failure in split phase`; зрелый (1 550 «favourites»);
единственный реалистичный способ **измерить потолок** «сколько скрыто геометрией». Минусы: baked → привязка к
координатам, **прямой конфликт с FO** (нужен пересчёт/ребейз данных, не заявлено); время бейка на 1.4×1.1 км и
4 949 дверях неизвестно (наш Umbra-бейк падал/висел); лицензия на место.

**Drakkar Culling Pro** — карточка `…/drakkar-culling-pro-323413`; ещё одна окклюзия «под ключ», издатель отдельно
предупреждает, что **не тестировал VR/XR** [LIKELY — сниппет]. Цена/дата/совместимость **[UNVERIFIED]** → не приоритет.

**SmartCuller Pro** — `…/smartculler-pro-advanced-system-optimizer-322806`: «runtime culling **without requiring
colliders**», frustum + occlusion, автоматическое управление видимостью [LIKELY — сниппет]. Это класс «то же, что
напишем сами за неделю»; цену/дату надо проверять.

**RT Occlusion Culling (VisualWorks)** — `…/rt-occlusion-culling-dynamic-occlusion-culling-138442`: динамическая
окклюзия «через рендер» [LIKELY]; цена/дата **[UNVERIFIED]**. **InstantOC (Frenchfaso)** —
`…/instantoc-dynamic-occlusion-culling-lod-6391`: **легаси** (обсуждение 2017–2019, v2.2.0) [LIKELY]. Оба —
дополнительный проход/рейкасты и/или легаси-статус → не рекомендую.

**SECTR (Procedural Worlds)** — в магазине сейчас **SECTR World Streaming for Unity 6**: **$99**, v**2019.0.11**,
релиз **2026-09-19**, 97.1 МБ, строки `2022.3.62f3` + `2019.4.0f1` (все пайплайны Compatible)
[VERIFIED: `…/sectr-world-streaming-for-unity-6-144433`]. Исторически SECTR COMPLETE включал **VIS — портальный
dynamic occlusion без статик-флагов и без бейка** [LIKELY: `procedural-worlds.com/products/professional/sectr/`,
анонс `discussions.unity.com/t/…/529693`]. Плюсы: концептуально идеален для домов/комнат/интерьеров кораблей, не
требует запекания → потенциально FO-совместим. Минусы (**почему не топ**): (1) порталы/секторы надо **авторить на
каждый из 2 161–2 363 проёмов** — недели скриптовой разметки; (2) продаётся **стриминг-пакет**, наличие VIS и его
состав в текущей версии **[UNVERIFIED]**; (3) стриминг **дублирует** нашу работающую схему; (4) 97 МБ, родословная
Unity 2017.

### 2.2 Инстансинг / GPU-driven (платные)

**GPU Instancer Pro (GurBu Technologies)** — **$128**, v**0.12.20**, релиз **2026-10-07**, 14.0 МБ; строка
`2022.3.32f1` — все пайплайны Compatible; поддержка на wiki.gurbu.com [VERIFIED: `…/gpu-instancer-pro-290293`].
Старая **GPU Instancer 1.8.5 снята с продажи** (deprecated, новые покупки невозможны) [VERIFIED:
`…/gpu-instancer-117566`]. Механизм: косвенный GPU-инстансинг (compute buffers, instanced draws) + GPU-culling
(frustum/occlusion) + менеджеры prefab/terrain/detail, оптимизирован под **много одинаковых мешей**
[LIKELY — ключевые слова страницы: occlusion, culling, compute, instancing]. Плюсы: зрелая технология ровно для
«много одинаковых», occlusion считается на GPU, релизы 2026. Минусы/риски: **версия 0.x**; Unity 6.5/URP 17.5 не
заявлены; для дверей нужна **дедупликация мешей**; при FO матрицы инстансов надо **re-push после ребейза**
[UNVERIFIED, критично]; кастомные шейдеры (`ProjectC/ProceduralSteel`) должны поддерживать инстансинг — иначе та же
ошибка, что 18.10 с GPU Resident Drawer.

### 2.3 Комбайнеры / упрощение (платные)

**Mesh Baker (Ian Deane)** — **$79**, v**3.40.2**, релиз **2026-06-26**, 65.5 МБ; строки `2022.3.5f1` +
`2019.4.0f1` (все пайплайны) [VERIFIED: `…/mesh-baker-5017`]. Механизм: объединение мешей + атласирование
текстур/материалов, в т.ч. skinned [LIKELY]. **Вердикт: анти-цель.** Мы уже прошли «147 715 объектов → 1 207
рендереров» и упёрлись в потерю гранулярности; Mesh Baker — следующий шаг туда же. Осмыслен только для дальнего
HLOD-тира (>800 м) вместе с LOD-системой, а не вместо куллинга.

**Poly Few (Brain Fail Productions)** — **$79.99**, v**7.86**, релиз **2026-07-20**, 19.8 МБ; **таблицы совместимости
на странице нет** [VERIFIED: `…/poly-few-…-160139`]. Механизм: упрощение мешей + авто-LOD + комбайнинг материалов
[LIKELY]. Решает **треугольники и вес**, а не 17 766 draw calls: при выключенных дверях кадр рисует 10.88 млн тр. при
35.7 FPS — лимитер не треугольники. Полотно двери 0.52 × 2.30 × 0.07 упрощать нечего → не приоритет.

### 2.4 LOD / импосторы

**Amplify Impostors (Amplify Creations)** — **$60**, v**1.1.0**, релиз **2026-10-28** (самый свежий), 207.2 МБ;
строка `2022.3.22f1` — все пайплайны [VERIFIED: `…/amplify-impostors-119877`]. Механизм: запекает меш в текстуры
и рисует **сильно упрощённой плоскостью** со специальным рантайм-шейдером; есть мульти-объектный бейк
[LIKELY: `wiki.amplify.pt/…/Amplify_Impostors/Manual`, `…/Tutorials/Multi_Object_Bake`]. Это **не culling, а LOD-тир**:
полезен только с `DistantFocusRenderFeature` (FarStart 800 / FarEnd 4000) для дальнего силуэта; требует бейка в
**локальных** координатах (иначе FO ломает); импостор не отсекает других.

**Smart LOD / BT-OptiMeshX / LOD Generator & Mesh Simplifier / LODify** — Smart LOD легаси (анонс 2017, $15)
[LIKELY: `discussions.unity.com/t/smart-lod-released/484235`]; остальные карточки существуют (315648, 237010, 325106),
но **[UNVERIFIED]**. Категория «генератор LOD» не закрывает ни одну из 4 болей; при 0 `LODGroup` в проекте генерацию
можно написать самим на `Whinarn/UnityMeshSimplifier` (MIT).

### 2.5 Free / open-source (GitHub API, 2026-10-30)

| Репозиторий | Лицензия | ★ | Push | Что это | Годность |
|---|---|---|---|---|---|
| `Unity-Technologies/com.unity.virtualmesh` | **Other (NOASSERTION)** | 176 | 2026-01-27 | Официальный пакет: virtual geometry, GPU-driven, **cluster LOD + двухпроходная окклюзия на уровне треугольников**, **только статические объекты** [VERIFIED по описанию] | Ближайшее к «идеалу», но static-only + лицензия неясна → наблюдать |
| `przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling` | **нет лицензии** | 251 | 2023-06-26 | GPU-окклюзия (демо) | Код брать **нельзя** (нет лицензии), 2023 |
| `Milk-Drinker01/Milk_Instancer01` | MIT | 139 | 2023-05-08 | Indirect instancing + occlusion/frustum + LOD | Хороший **референс** для своего инстансера |
| `MangoButtermilch/Unity-Grass-Instancer` | MIT | 340 | 2026-05-15 | Инстансинг травы + occlusion | Только растительность |
| `FerzanK/Horus-Unity-Occlusion-Culling` | MIT | 41 | 2020-01-02 | Occlusion culling (C++ плагин) | Легаси |
| `arcsearoc/HiZ-Occlusion-Culling-Learning`, `walkerxian/Hi-Z_GPU_Occlusion_Culling` | нет / MIT | 4 / 1 | 2025-08 / 2023-03 | Учебные Hi-Z демо | Не продакшн |
| `Unity-Technologies/MaskedOcclusionCulling` | — | — | — | Ожидаемый официальный репо | **HTTP 404** [VERIFIED] — путь неверен/репо удалён |

**Вывод по OSS:** «готовый Hi-Z под Unity 6 + URP 17.5 + FO» **не существует**. Есть учебные демо, старые демо без
лицензии и официальный `virtualmesh` (static-only, лицензия сомнительна). Реалистичный путь — свой куллер на встроенных
API, а OSS использовать как **референс алгоритмов**.

### 2.6 Встроенное (кратко; детали — сабагент A)

| Механизм | Что делает | Наш статус | Годность |
|---|---|---|---|
| **Umbra** | baked-окклюзия по статик-флагам | 6 бейков провалились, данных 0; static-флаги запрещены FO | **Нет** [VERIFIED локально] |
| **GPU Resident Drawer** | BRG-инстансинг: indirect draw на группу (mesh, material) | был включён 17.10, откачен 18.10 (шейдеры без DOTS-инстансинга) | **Да — главный бесплатный кандидат**; док: `docs.unity3d.com/6000.0/Documentation/Manual/urp/reduce-rendering-work-on-cpu.html` |
| **GPU occlusion culling (URP)** | GPU отсекает окклюдированное по depth; работает вместе с Drawer, ограничения те же | выключен (`…EnableOcclusionCullingInCameras: 0`) | **Да, если включить GPU-путь**; док: `…/Manual/urp/gpu-culling.html` |
| **CullingGroup** | сферы: видимость из камер + дистанционные бэнды + `onStateChanged`, `IsVisible`, `GetDistance`, `QueryIndices`, `SetBoundingSpheres`, `targetCamera` | не используется | **Да — идеальный FO-safe блок** [VERIFIED: API 6000.0] |
| **LODGroup** | LOD по экранному размеру | 0 LODGroup | Да, но это отдельный пайплайн |
| **SRP Batcher** | меньше state changes, **не** объединяет вызовы | включён | Да (уже) |
| **GPU instancing (галочка)** | инстансинг одинаковых мешей | рекомендация Unity для URP — **выключить** | Нет как основной путь |

Ограничения GPU-окклюзии URP (док `gpu-culling`): наиболее эффективна, когда **много объектов с одним мешем**, в сцене
**много перекрытий** и у скрытых объектов **малый экранный радиус** [LIKELY — сниппет доки]. Для дверей это наш случай
**после дедупа**. Отдельно: Entities не могут использовать GPU Resident Drawer, а occlusion-колбэки **жёстко привязаны**
к нему → свой double-pass occlusion на кастомном BRG без правки пайплайна не сделать [LIKELY:
`discussions.unity.com/t/occlusion-culling-in-ecs/1704320`].

---

## 3. Сравнение / матрица

### 3.1 Ассет → 4 боли → цена → вердикт

| Решение | Двери | Город после склейки | FO / стриминг | Корабли | Цена | Вердикт |
|---|---|---|---|---|---|---|
| **GPU Resident Drawer + GPU occlusion** (встроено) | **Да, после дедупа** | Да (меньше CPU) | **Да** — нет baked-данных | Да | **$0** | **P1** |
| **CullingGroup (свой куллер)** (встроено) | Да (бэнды/видимость) | Да | **Да** — frame-local | Да | **$0** | **P1** |
| **GPU Instancer Pro** | Да-но: после дедупа | Частично (пропсы, фермы) | Риск: re-push матриц при ребейзе | Нет | **$128** | **P2** триал |
| **Perfect Culling** | Да как «измеритель потолка» | Да | Риск: baked в мире | Да | **$107.21** | **P3** триал |
| **Advanced Culling System 2** | Возможно (dynamic) | Да | [UNVERIFIED] | Да | **$69.95** | P3, если есть время |
| **SECTR** | Да, но порталы на 2 161 проём | Нет | Стриминг дублирует наш | Да (интерьеры) | **$99** | **Нет** |
| **Mesh Baker** | Нет | **Анти-цель** | — | Нет | **$79** | **Нет** |
| **Poly Few** | Нет | Частично (тр., не DC) | — | Нет | **$79.99** | **Нет** |
| **Amplify Impostors** | Нет | Только дальний тир | Требует локальных координат | Нет | **$60** | **Позже** (с LOD) |
| **Drakkar / SmartCuller / RT Occlusion / InstantOC** | Возможно | Возможно | [UNVERIFIED] | — | [UNVERIFIED] | **Нет** |
| **OSS Hi-Z** | Нет | Нет | — | — | $0, но лицензии/легаси | **Нет как код**; да как референс |
| **`com.unity.virtualmesh`** | Нет (static) | Да, но static-only | неизвестно | Нет | лицензия неясна | **Наблюдать** |

### 3.2 Что ассеты физически **не могут** сделать у нас

1. **Куллер не создаёт то, что можно куллить:** 4 949 отдельных `MeshRenderer` (`static = false`) — окклюзия отсекает
   рендерер целиком, ровно как frustum culling Unity. Выигрыш только у **скрытых** дверей (прокси-оценка: 99 %
   во фрустуме «невидимы» по физике — это **оценка**, потолок не доказан).
2. **Baked в мире ломается о ребейз:** ни один проверенный ассет **не заявляет** FO-совместимость.
3. **Инстансинг требует одинаковых мешей:** пока 2 363 двери — уникальные меши, ни GPUI, ни Drawer не дадут выигрыша.
   **Дедуп — предусловие, а не следствие покупки.**
4. **Тени считаются отдельно:** двери дороги как shadow casters (26 981 DC в конфиге A; 21 118 в B; без дверей
   3 352–4 954). Ни один ассет не заявляет отдельный shadow-caster culling для нестатических — проверять в триале.

---

## 4. Что это значит для Project C

**Двери (17 766 из 21 118 = 84 %).** Боль = 22 027 draw calls (конфиг A, «двери вкл/выкл») и 17 766 (конфиг B)
[VERIFIED: `04_OCCLUSION_CULLING_RESEARCH.md` §3]; 2 363 инстанса × 2 рендерера, `static = false`, 4 432 `BoxCollider`,
полотно 0.52 × 2.30 × 0.07, интерактива нет [VERIFIED: `04_COLLIDERS`]. **Никакой culling-ассет не даст «−17k» сам по
себе** — это 17 766 разных рендереров, которые Unity и так куллит; реальный потолок — «сколько скрыто». Порядок
величины иначе даёт инстансинг: если полотна одинаковы (одинаковые размеры в коллайдерах намекают), 2 363 двери →
1–3 indirect draw на (mesh, material), и кадр уходит к ~3.4–5 k DC [ОЦЕНКА: 21 118 − 17 766 + ~10…50].
**Первый шаг — измерение, не покупка:** хэш `sharedMesh` + `sharedMaterial` по всем 4 949 рендерерам дверей.

**Город после склейки (1 207 рендереров, 761 батч).** Склейка дала −98 % объектов, но 761 единица отсечения на
1.4 × 1.1 км — это нормально для окклюзии/дистанции и **плохо для LOD** (нельзя упростить «дальний квартал» отдельно).
Вывод: склейка **не была ошибкой**, ошибкой была бы **дальнейшая** склейка; текущая гранулярность рабочая.
Дальний тир (800–4000 м) — отдельный HLOD/impostor пайплайн, не culling-ассет.

**Floating Origin и стриминг.** Требование к любому решению: **никаких данных видимости в мировых координатах**.
Допустимо frame-local (CullingGroup, frustum, GPU-окклюзия по depth) или данные в локальных координатах ячейки с
пересчётом при ребейзе. CullingGroup и GPU-окклюзия удовлетворяют по конструкции (сферы/матрицы из трансформов, а
трансформы едут с корнями). Любой baked-ассет = новый **участник ребейза** по нашему пайплайну
(`floatingorigin/09S_NEW_CONTENT_PIPELINE.md`) + тест F8. Стриминг у нас уже есть (`ClientSceneLoader`,
`WorldStreamingManager`) — покупать SECTR-стриминг = рисковать архитектурой без выигрыша.

**Корабли.** Корабли склеиваются `CombineMeshesToCollider` в `*_Combined.asset` + `MeshCollider`. Ключевой факт:
у инструмента есть режим **«No (только коллайдер)»** — рендеры детей **не удаляются**, добавляется только общий
коллайдер [VERIFIED: `Assets/_Project/Editor/CombineMeshesToCollider.cs`, строки 49–62]. Значит гипотезу «не склеивать
рендер, склеить только коллайдер» **можно проверить без нового кода** (100–150 draw calls на корабль, зато отсечение
палуб/трюма/интерьера по модулям + LOD на модуль). Покупной ассет не нужен.

---

## 5. Рекомендованные эксперименты

| № | Эксперимент | Мерить | Ожидание | Kill criteria |
|---|---|---|---|---|
| **E1** | **Дедуп-аудит дверей** (editor-скрипт: хэш `sharedMesh`+`sharedMaterial`) | число уникальных пар, распределение вершин | <50 пар → инстансинг жизнеспособен | >1 000 пар → инстансинг дверей закрыт |
| **E2** | **GPU Resident Drawer после починки шейдеров** (`DOTS_INSTANCING_ON`) | `UnityStats.drawCalls`, SetPass, Frame Debugger (Hybrid Batch Group) | падение в разы | шейдеры не чинятся за 2 дня → откат |
| **E3** | **Свой куллер на CullingGroup** (двери + `RuinsValleys`) | draw calls, CPU ms в Profiler | двери в кадре −70…90 % по дистанции | CPU > 1.5 ms/кадр на 5k сфер → чанк-уровень |
| **E4** | **Триал Perfect Culling** на под-сцене (не на 63 МБ целиком) | время бейка, размер данных, DC в 3 точках | покажет потолок окклюзии | бейк > 30 мин или падение → не наш |
| **E5** | **Триал GPU Instancer Pro** на дедуплицированных дверях (если E1 успешен) | DC, F8-тест, тени | 17.7k → сотни | матрицы не переживают ребейз → P2 закрыт |
| **E6** | **Корабль в режиме «только коллайдер»** | DC, гранулярность, FPS | +100–150 DC, зато куллинг по модулям | DC растут непропорционально → вернуть склейку |

**Чек-лист любого триала:** (1) Unity **6000.5.2f1** + URP **17.5.0**, наш Forward, не только Forward+;
(2) non-static объекты — без требования `static`-флагов; (3) **F8-тест** ребейза: объекты на месте, 0 errors,
видимость не «залипла»; (4) multi-scene (`WorldScene_0_0` + аддитивная `BootstrapScene`); (5) тени отдельным замером
в обоих конфигах (1000/2 и 250/1) — иначе 84 % боли остаётся; (6) CPU в Profiler до/после на равном `frameCount`
(FPS первых ~1 500 кадров врёт); (7) не растёт ли SetPass (совместимость с SRP Batcher); (8) память данных видимости;
(9) лицензия на N мест и право commercial-использования.

---

## 6. Риски и открытые вопросы

1. **Механика платных ассетов не подтверждена первоисточником** (описания на страницах не читаются). Критично
   проверить вручную: ACS2 (как работает dynamic-модуль), Perfect Culling (где хранятся данные ячеек), GPUI Pro
   (есть ли ручное обновление матриц под FO).
2. **Unity 6.5 / URP 17.5 не заявлены ни одним платным ассетом**; самые свежие релизы (Amplify 2026-10-28,
   ACS2 2026-09-02) всё равно имеют таблицу совместимости 2022.3.
3. **Экономика:** все — Standard EULA, «Extension Asset», **по местам**; $128 × N разработчиков при ограниченной ценности.
4. **Лицензии OSS:** два самых релевантных репо (251★ и учебное Hi-Z) — **без лицензии** → код брать нельзя;
   `com.unity.virtualmesh` — `NOASSERTION`.
5. **Риск «купили и не завелось на Unity 6»** — самый вероятный сценарий; поэтому приоритет у встроенного стека и
   своего кода, а покупки — только триал с kill criteria.
6. **Потолок окклюзии на нашей сцене не доказан** (прокси-оценка по коллайдерам методологически слабая).
7. **`com.unity.virtualmesh`** может оказаться ответом для статического дальнего города — но static-only, молодой
   (создан 2025-10-21) и с неясной лицензией; статус надо перепроверить отдельно (без web_search не смог).

---

## 7. Источники (дата обращения ко всем — 2026-10-30)

**Asset Store — страницы открыты (цена/версия/дата/лицензия VERIFIED):**
1. GPU Instancer Pro — https://assetstore.unity.com/packages/tools/utilities/gpu-instancer-pro-290293 — $128, v0.12.20,
   2026-10-07, orig 2022.3.32, URP-compat 2022.3.32f1.
2. GPU Instancer 1.8.5 (deprecated) — https://assetstore.unity.com/packages/tools/utilities/gpu-instancer-117566.
3. ACS2 — https://assetstore.unity.com/packages/tools/utilities/advanced-culling-system-2-complete-optimization-solution-272830
   — $69.95, v1.5.8, 2026-09-02, orig 2022.3.62.
4. Perfect Culling — https://assetstore.unity.com/packages/tools/utilities/perfect-culling-occlusion-culling-system-193611
   — $107.21, v1.3.0.4, 2026-02-12, orig 2019.4.41.
5. SECTR World Streaming for Unity 6 — https://assetstore.unity.com/packages/tools/terrain/sectr-world-streaming-for-unity-6-144433
   — $99, v2019.0.11, 2026-09-19, orig 2017.4.36.
6. Amplify Impostors — https://assetstore.unity.com/packages/tools/utilities/amplify-impostors-119877 — $60, v1.1.0,
   2026-10-28, orig 2022.3.22.
7. Mesh Baker — https://assetstore.unity.com/packages/tools/modeling/mesh-baker-5017 — $79, v3.40.2, 2026-06-26,
   orig 2019.4.0.
8. Poly Few — https://assetstore.unity.com/packages/tools/utilities/poly-few-mesh-simplifier-and-auto-lod-generator-160139
   — $79.99, v7.86, 2026-07-20, orig 2022.3.62.
   Лицензия всех восьми: Standard Unity Asset Store EULA (`https://unity.com/legal/as-terms`), Extension Asset, «Seats».

**Asset Store — карточки найдены в выдаче, страницы не открыты (UNVERIFIED):**
Drakkar Culling Pro `…/packages/tools/game-toolkits/drakkar-culling-pro-323413`; SmartCuller Pro
`…/tools/utilities/smartculler-pro-advanced-system-optimizer-322806`; RT Occlusion Culling
`…/tools/camera/rt-occlusion-culling-dynamic-occlusion-culling-138442`; InstantOC `…/tools/camera/instantoc-dynamic-occlusion-culling-lod-6391`;
MeshFusion + ACS Add-on `…/tools/utilities/meshfusion-advanced-culling-integration-add-on-346852`; BT-OptiMeshX
`…/tools/utilities/bt-optimeshx-all-in-one-smart-lod-mesh-model-optimization-system-315648`; LOD Generator & Mesh Simplifier
`…/tools/utilities/lod-generator-mesh-simplifier-237010`; LODify `marketplace.unity.com/packages/3d/props/tools/lodify-…-325106`.

**Документация/обсуждения Unity:** CullingGroup (`docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/cullinggroup`,
API VERIFIED); GPU Resident Drawer (`docs.unity3d.com/6000.0/Documentation/Manual/urp/reduce-rendering-work-on-cpu.html`);
GPU occlusion culling (`…/Manual/urp/gpu-culling.html`); границы для Entities/BRG
(`discussions.unity.com/t/occlusion-culling-in-ecs/1704320`, LIKELY); SECTR VIS анонс
(`discussions.unity.com/t/released-sectr-vis-dynamic-occlusion-culling/529693`), SECTR у издателя
(`procedural-worlds.com/products/professional/sectr/`); ACS2 анонс
(`discussions.unity.com/t/released-advanced-culling-system-revamping-and-boosting-up/924820`); Poly Few обсуждение
(`discussions.unity.com/t/poly-few-mesh-simplifier-and-automatic-lod-generator/772962`); Amplify Impostors вики
(`wiki.amplify.pt/index.php?title=Unity_Products:Amplify_Impostors/Manual`); Smart LOD анонс 2017
(`discussions.unity.com/t/smart-lod-released/484235`).

**GitHub (метаданные через `api.github.com/search/repositories`):** `Unity-Technologies/com.unity.virtualmesh` (176★,
NOASSERTION, push 2026-01-27); `przemyslawzaworski/Unity-GPU-Based-Occlusion-Culling` (251★, нет лицензии, 2023-06-26);
`Milk-Drinker01/Milk_Instancer01` (MIT, 139★, 2023-05-08); `MangoButtermilch/Unity-Grass-Instancer` (MIT, 340★,
2026-05-15); `FerzanK/Horus-Unity-Occlusion-Culling` (MIT, 41★, 2020-01-02); `arcsearoc/HiZ-Occlusion-Culling-Learning`
(нет лицензии, 4★, 2025-08-27); `walkerxian/Hi-Z_GPU_Occlusion_Culling` (MIT, 1★, 2023-03-26);
`Unity-Technologies/MaskedOcclusionCulling` → HTTP 404.

**Локальные (ground truth по нашим числам):** `10_CULLING_RESEARCH/_BRIEF.md`;
`06_REFERENCE/04_OCCLUSION_CULLING_RESEARCH.md` (замеры DC, провал Umbra); `06_REFERENCE/01_INDUSTRY_PRACTICE.md`
(GPU-путь, откат 18.10); `04_COLLIDERS/*.md` (двери); `Assets/_Project/Editor/CombineMeshesToCollider.cs` (корабли).

---

## 8. Приложение

Машиночитаемая таблица: `08_DATA/B_ASSETS_TABLE.csv` (имя, вендор, тип, цена, лицензия, версия, дата, min Unity, URP,
Unity 6 явно, dynamic, baked, FO-safe, multi-scene, тени, снижение DC, URL, дата обращения, достоверность, вердикт).
Поиск: `https://lite.duckduckgo.com/lite/?q=…`. GitHub-метаданные:
`GET https://api.github.com/search/repositories?q=<query>&sort=stars&per_page=5` (unauthenticated). Проверка карточки:
`GET https://assetstore.unity.com/packages/...` → поля `Latest version`, `Latest release date`, `Original Unity version`,
цена, таблица совместимости; `Description`/`Technical details` пустые. Все «17 766 → десятки» — **арифметика от наших
замеров**, помечена `[ОЦЕНКА]`.

---

## Handoff

- **Главный вывод:** покупать culling-ассет под нашу задачу не нужно и рискованно; 84 % боли снимается дедупликацией
  мешей дверей + инстансингом/GPU Resident Drawer, остаток — свой куллер на `CullingGroup` (FO-safe, $0).
- **Факт 1:** ни один проверенный платный ассет не декларирует Unity 6.x/URP 17.5 — только 2019.4/2022.3 [VERIFIED].
- **Факт 2:** GPU Instancer 1.8.5 снят с продажи → актуален только Pro 0.12.20 ($128, релиз 2026-10-07) [VERIFIED].
- **Факт 3:** официальный Unity-пакет virtual geometry (`com.unity.virtualmesh`) делает «GPU-driven + двухпроходную
  окклюзию + cluster LOD», но **static-only** и с неясной лицензией [VERIFIED по описанию репозитория].
- **Не проверено:** механизмы всех платных ассетов (описания не читаются фетчером); FO-совместимость любого из них;
  фактический потолок окклюзии на нашей сцене; лицензия/статус `com.unity.virtualmesh`; жив ли `MaskedOcclusionCulling`
  под другим именем.
