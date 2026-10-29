# 06 — Ореолы и flex-замены: что снято в моде и как это переносится

> Источник — `plan/04_halos.md` мода `v4-2` (папка `.Aura/Mods/` **не** попадает в git, поэтому таблицы
> продублированы здесь). Шаг 1 плана (`04` §4) выполнен: `box-shadow`, `::before`, `::after`, `display:grid`,
> `grid-template*`, `gap`, `outline` в моде больше не встречаются.
> Все цвета — sRGB с альфой; стопы — проценты от размера ореола; размеры — CSS px (device-independent),
> то есть те же числа, что нужны USS на опоре 1920×1080.

## 1. Механизм мода — в порт НЕ переносится

| Файл | Что делает | Почему не переносится |
|---|---|---|
| `src/halos.js` | `HALO_RULES` (селектор хозяина → список ореолов), `decorate(root)`, `MutationObserver` — вставляет детей `.halo` | В UITK дети создаются один раз в C#-дереве; обход DOM не нужен |
| `src/flex.js` | по CSSOM собирает группы с объявленным `--g`, вешает `.grp-*`, `.mgN`, `.is-first-in-group`, `.in-col-N` | CSS-переменных и CSSOM в USS нет; классификация должна быть зашита в C# при построении |
| `--g: Npx` у группы | объявленный шаг между детьми (74 группы) | в USS — литерал `margin` у ребёнка |

Правило переноса: **у каждой группы из `--g: Npx` дети получают `margin-right` (или `margin-bottom`) `Npx`,
первый ребёнок группы — `0`.** Позиция ребёнка в строке переноса (`.in-col-N`, `.is-first-in-line`,
`.is-in-first-line`) в C# считается при построении и зашивается классами; `:nth-child()` и `+` в USS запрещены.
Значения `--g` берутся из `src/*.css` поиском `--g:` — они буквальные, в px.

## 2. Базовые классы ореолов

| класс | что рисовало | геометрия | стопы | радиус |
|---|---|---|---|---|
| `.halo` | служебный предок | `position:absolute; display:block; pointer-events:none; border:0 solid transparent` | — | — |
| `.halo-sh` | внешнее размытие (тень с blur/spread либо смещением): 4 полосы по краям, середина прозрачна | `top:-var(--ht); bottom:-var(--hb); left/right:-var(--hs)`; 4 фоновых полосы `100% var(--ht)`, `100% var(--hb)`, `var(--hs) 100%` ×2 | на полосе `0% = var(--hc)`, `100% = rgba(0,0,0,0)` | 1–2 px |
| `.halo-rad` | круглое мягкое свечение точки | `inset: -var(--hg)`; `border-radius:50%` | `radial: transparent 0…40%, var(--hc) 100%` | 50% |
| `.halo-in` | внутреннее размытие (бывшая `inset`-тень) | `inset:0`; `border-radius:inherit` | `radial ellipse at center: transparent 0…46%, var(--hc) 100%` | inherit |

`--hs` — разлёт по бокам, `--ht`/`--hb` — вверх/вниз (сюда разложено бывшее смещение `dy`), `--hg` — разлёт
круга, `--hc` — цвет с альфой. Альфа монотонна от `a` на кромке хозяина к `0` на внешней кромке, поэтому
в C# рампа собирается как `lerp(alpha, a, 0)` по нормированному расстоянию — без гауссова ядра.

## 3. Внешние ореолы (`.halo-sh`): `--hs / --ht / --hb`

| хозяин → ореол | цвет | состояние |
|---|---|---|
| `.tip-card > .halo-tipsh` `26/16/36` | `rgba(0,0,0,.45)` | всегда |
| `.ln-card > .halo-cardsh` `26/16/36` | `rgba(0,0,0,.45)` | всегда |
| `.rp-menu > .halo-mnush` `34/20/48` | `rgba(0,0,0,.55)` | всегда |
| `.bg-pop > .halo-popsh` `34/20/48` | `rgba(0,0,0,.55)` | всегда |
| `.sh-pop > .halo-shsh` `34/20/48` | `rgba(0,0,0,.55)` | всегда |
| `.rw-fly > .halo-flysh` `40/24/56` | `rgba(0,0,0,.55)` | всегда |
| `#bag.open > .halo-bagsh` `40/54/26` | `rgba(0,0,0,.5)` | `#bag.open` |
| `.cal-more > .halo-calsh` `38/22/54` | `rgba(0,0,0,.62)` | всегда |
| `.ch-card > .halo-chsh` `46/28/64` | `rgba(0,0,0,.6)` | всегда |
| `.hm-slot > .halo-hmslot` `12/12/12` | `rgba(120,184,145,.55)` | `opacity 0→1` при `.hl`, `transition: opacity .15s` |
| `.sk-slot > .halo-skslot` `12/12/12` | `rgba(120,184,145,.55)` | `opacity 0→1` при hover/`.hl`; `.sk-slot.empty:hover` → `0`, `.15s` |
| `.st-node > .halo-stglow` `18/18/18` | `rgba(120,184,145,.6)` | `opacity 0→1` при `.st-cur`, `.2s` |
| `.st-cur.st-block .st-node > .halo-stglow` `16/16/16` | `rgba(194,91,69,.55)` | `opacity 1` |
| `.cal-cell > .halo-calglow` `6/6/6` | `rgba(120,184,145,.55)` | `opacity 0→1` при `.glow` |

## 4. Внутренние ореолы (`.halo-in`)

| хозяин → ореол | цвет | радиус | состояние |
|---|---|---|---|
| `.hm-niche > .halo-nichen` | `rgba(47,124,80,.16)` | `2px` | всегда |
| `.bg-pop > .halo-popin` | `rgba(47,124,80,.14)` | `2px` | всегда |
| `.sh-pop > .halo-shin` | `rgba(47,124,80,.14)` | `2px` | всегда |
| `.tip-card > .halo-tipin` | `rgba(47,124,80,.14)` | `2px` | всегда |
| `.rw-fly > .halo-flyin` | `rgba(47,124,80,.16)` | `2px` | всегда |
| `.rn > .halo-rnin` | `rgba(7,10,9,.8)` | inherit | всегда |
| `.skn > .halo-sknin` | `rgba(7,10,9,.8)` | inherit | всегда |
| `.rn-note > .halo-notein` | `rgba(7,10,9,.7)` | `2px` | всегда |
| `.rn-note.min > .halo-notein` | `rgba(141,122,78,.4)` | `2px` | `opacity .62` + `ntpulse 2.4s ease-in-out infinite` |

## 5. Канты, волоски и полосы (бывшие псевдоэлементы)

| хозяин → ореол | геометрия | цвет | состояние |
|---|---|---|---|
| `.ln + .ln > .ln-hair` (бывш. `::before`) | `left:0; right:0; top:0; height:1px` | `rgba(74,87,76,.22)` | `display:none → block` у каждой не первой строки |
| `.lb-plate > .halo-cap` (бывш. `::after`) | `left:0; right:0; bottom:-1px; height:1px` | `#b9915a` | `scaleX(0→1)`, origin `left center`, `.16s ease-out`; `.off` → `display:none` |
| `.lb-plate > .halo-smoke` | `left:2px; right:2px; bottom:-3px; height:3px` | `linear 90°: rgba(238,243,239,0) 0%, .42 50%, 0 100%` | `:active` → `lb-smoke .08s ease-out 1` |
| `.bg-navc/.bg-sort/.bg-cat > .halo-bgcap` | `left:0; right:0; bottom:-1px; height:1px` | `#b9915a` | `.on` → `block` |
| `.bg-tile > .halo-tilebar` | `left:0; top:0; bottom:0; width:2px` | `#78b891` | `.on` → `block` |
| `.qs-row > .halo-qsbar` | `left:0; top:0; bottom:0; width:2px` | `#78b891` | `.on` → `block` |
| `.qs-tab/.qs-chip/.qs-act > .halo-qscap` | `left:0; right:0; bottom:-1px; height:1px` | `#8d7a4e` | `.on` → `block` |
| `.cw-tag/.cw-stab/.cw-sort > .halo-cwcap` | `left:0; right:0; bottom:-1px; height:1px` | `#8d7a4e` | `.on` → `block` |
| `.cw-srow > .halo-cwbar` | `left:0; top:0; bottom:0; width:2px` | `#78b891` | `.on` → `block` |
| `.rw > .halo-rwhair` | `left:0; top:4px; width:1px; height:14px` | `#b9915a` при `opacity .6` | всегда |
| `.rw-fly > .halo-rfhair` | `left:0; top:10px; width:1px; height:18px` | `#b9915a` при `opacity .6` | всегда |
| `.rn-note > .halo-rnhair` | `left:10px; right:10px; top:4px; height:1px` | `#8d7a4e` при `opacity .55` | `.rn-note.min` → `display:none` |
| `.cal > .halo-calhair` | `left:-1px; top:5px; width:1px; height:13px` | `#8d7a4e` при `opacity .65` | всегда |
| `.skn-root > .halo-roothair` | строчный: `12×1px; margin-right:5px; vertical-align:3px` | `rgba(185,145,90,.85)` | всегда (единственный ореол в потоке) |
| `.ln-card > .halo-arrow` | `top:-8px; left:calc(var(--ax) - 16px); border-width:0 8px 8px`, боковые прозрачны | `rgba(96,150,116,.6)` | всегда |
| `.tip-card > .halo-tipc1` | `9×9px; left:-1px; top:-1px` (без правой и нижней границы) | `#8d7a4e` при `opacity .55` | всегда |
| `.tip-card > .halo-tipc2` | `9×9px; right:-1px; bottom:-1px` (без левой и верхней) | `#8d7a4e` при `opacity .55` | всегда |

`--ax` у стрелки карточки — вычисляемая позиция из геометрии карточки (в C# — поле, не CSS-переменная).

## 6. Кольца и рамки (border-ореолы)

| хозяин → ореол | геометрия | цвет | состояние |
|---|---|---|---|
| `.hm-cr > .halo-hmcr` | `inset:0; border-width:1px` | `rgba(74,87,76,.7)` / `.hl` → `#78b891` | `.hl` |
| `.st-node > .halo-string` | `inset:-4.5px; border-width:3px; radius 50%` | `rgba(7,10,9,.55)` | всегда |
| `.st-node > .halo-xring` | `inset:-6px; border-width:1.5px dashed; radius 50%` | `#8d7a4e` | `.st-xfer` → `block` |
| `.st-node > .halo-open` | `inset:-8.5px; border-width:2px; radius 50%` | `#eef3ef` | `.st-open` → `block` |
| `.end-node > .halo-endring` | `inset:-5px; border-width:3px; radius 2px` | `rgba(7,10,9,.6)` | всегда |
| `.end-node > .halo-endfill` | `inset:5px; radius 2px` | `var(--lc)` при `opacity .55` | всегда |
| `.end-node > .halo-endspark` | `inset:-2px; border-width:3px; radius 2px` | `rgba(120,184,145,.9)` при `opacity 0` | `.ln.finishing` → `won .9s ease-out 2` |
| `.lb-riv > .halo-riv` | `inset:-1px; border-width:1px; radius 50%` | `rgba(96,150,116,.45)` | всегда |
| `.bg-tile > .halo-hit` | `inset:0; border-width:1px; radius 2px` | `rgba(185,145,90,.85)` при `opacity 0` | `.bg-hit` → `bghit-hl .7s` |
| `.rw-ring > .halo-rwring` | `inset:0; border-width:1px; radius 50%` | прозрачный / `.ghosted` → `rgba(120,184,145,.35)` | `.ghosted` |
| `.rw-ring > .halo-rwpulse` | `inset:0; border-width:2px; radius 50%` | `rgba(120,184,145,.9)` | `.pulse` → `rwpulse 1.1s` |
| `.rw-ring > .halo-rwtier` | `inset:0; border-width:2px; radius 50%` | `rgb(196,224,205)` | `.tierflash` → `rwtier 1.6s` |
| `.rl-tsw > .halo-tsw` | `inset:-1px; border-width:1px dashed; radius 1px` | `#a8b6ac` | `.rl-tier.closed` → `block` |
| `.skn > .halo-sknring` | `inset:0; border-width:1px` | прозрачный / `.root` → `rgba(185,145,90,.75)` | `.root` |
| `.cal-cell > .halo-calring` | `inset:0; border-width:1px; radius 1px` | прозрачный / `.hl` → `#b9915a` | `.hl` |
| `.cal-cell > .halo-calpulse` | `inset:0; border-width:3px; radius 1px` | `rgba(120,184,145,.9)` | `.pulse` → `calpulse .9s` |
| `.cal-cell > .halo-calhl` | `inset:0; border-width:4px; radius 1px` | `rgba(185,145,90,.95)` | `.hlp` → `calhl .85s` |
| `.cm-d > .halo-cmsel`, `.cp-d > .halo-cpsel` | `inset:-1px; border-width:1px; radius 1px` | прозрачный / `.hl` → `#b9915a`, `.cp-d.sel` → `#8d7a4e` | `.hl`, `.sel` |
| `.cm-d/.cp-d > .halo-calhl2` | `inset:0; border-width:4px; radius 1px` | `rgba(185,145,90,.95)` | `.hlp` → `calhl2 .85s` |
| `.ch-hp > .halo-chhp` | `inset:0; border-width:1px; radius 2px` | `rgba(74,87,76,.7)` | `.flash` → `chflash .36s` |

## 7. Точка двигателя

| хозяин → ореол | геометрия | цвет | состояние |
|---|---|---|---|
| `.sh-eng > i > .halo-dotglow` (`halo-rad`) | `--hg:8px` | `rgba(120,184,145,.7)` | `.sh-eng.off` → `opacity 0` |

## 8. Анимации (переносятся C#-драйвером в `Update(dt)`, шаг 5)

| keyframes | длительность | easing | повторы | кадры | запуск |
|---|---|---|---|---|---|
| `rwpulse` | `1.1s` | `ease-out` | 1 | `0%: scale(1), opacity .9` → `100%: scale(1.64), opacity 0` | `.rw-row.pulse > .rw-ring > .halo-rwpulse` |
| `rwtier` | `1.6s` | `ease-out` | 1 | `0%,30%: scale(1), opacity .95` → `100%: scale(1.5), opacity 0` | `.rw-row.tierflash > .rw-ring > .halo-rwtier` |
| `calpulse` | `.9s` | `ease-out` | 1 | `0%: scale(1), opacity .9` → `100%: scale(2.07), opacity 0` | `.cal-cell.pulse > .halo-calpulse` |
| `calhl` | `.85s` | `ease-out` | 1 | `0%: scale(1), opacity .95` → `100%: scale(2.2), opacity 0` | `.cal-cell.hlp > .halo-calhl` |
| `calhl2` | `.85s` | `ease-out` | 1 | `0%: scale(1), opacity .95` → `100%: scale(1.9), opacity 0` | `.cm-d/.cp-d.hlp > .halo-calhl2` |
| `won` | `.9s` | `ease-out` | 2 | `0%: scale(1), opacity .9` → `100%: scale(2.69), opacity 0` | `.ln.finishing .end-node > .halo-endspark` |
| `ntpulse` | `2.4s` | `ease-in-out` | ∞ | `0%,100%: opacity .62` → `50%: opacity 1` | `.rn-note.min > .halo-notein` |
| `lb-smoke` | `.08s` | `ease-out` | 1 | `0%: translateY(0), opacity .75` → `100%: translateY(2px), opacity 0` | `.lb-plate > .halo-smoke` на `:active` |
| `bghit-hl` | `.7s` | `ease-out` | 1 | `0%: opacity .85` → `100%: opacity 0` | `.bg-tile.bg-hit > .halo-hit` |
| `chflash` | `.36s` | `ease-out` | 1 | `0%: border rgba(120,184,145,.9)` → `100%: border rgba(74,87,76,.7)` | `.ch-hp.flash > .halo-chhp` |

Длительности и easing — прежние, перенесены с теней один в один. Изменилась только анимируемая величина:
`box-shadow`/`transform` тени → `opacity` ореола; `scale` сохранён, потому что он тоже был частью геометрии.

## 9. Нерешённые случаи и ограничения (с числами)

1. `.ch-hp > .halo-chhp`: рамка есть (`inset:0`, `1px`), но правые **2 px** срезает `overflow:hidden` самого
   `.ch-hp` — 0 видимых px справа. Так было и до замены; геометрия не менялась.
2. `.halo-sh` — линейная аппроксимация размытия: 4 полосы по одному стопу (`0% / 100%`), гауссова спада нет.
   В C# это допустимо: рампа тоже линейная от `a` к `0`.
3. `.st-node > .halo-ear-l/-r` — единственная жёсткая кромка вместо радиального размытия:
   `radial-gradient(16px at 19px 6px, transparent 0 96%, #1f2620 97% 100%)` и зеркально `at 1px 6px` —
   кольцо `1px` на краю `20×20px`. Расхождение с бывшим радиальным свечением — 1–2 px по внешней кромке.
4. Смещение `dy` разложено на `--ht`/`--hb` (например `#bag.open`: `--ht:54px`, `--hb:26px`), поэтому ореол
   не сдвигает вёрстку и не выходит за `overflow`.
5. `text-shadow` — 9 вхождений, все **одиночные** (списков нет), в USS допустимы одна тень и размытие ≤ паддинга
   SDF, поэтому оставлены как есть. `filter` в CSS — 0 (единственное вхождение `filter:` — JS-ключ `dataset`
   в `quests.js`, не стиль). `outline` — 0: видимые рамки стали ореолами (`.halo-cpsel`, `.halo-swsel`),
   7 сбросов `outline: none` удалены, потому что в UITK фокус-рамки нет. `drop-shadow` — 0.

## 10. Проверка шага 1 (счётчики `findstr` по `src/*.js` и `src/*.css`)

| конструкция | до | после |
|---|---|---|
| `box-shadow` | 43 | **0** |
| `::before` | 10 | **0** |
| `::after` | 11 | **0** |
| `display:grid` / `display: grid` | 23 | **0** |
| `grid-template*` | 26 | **0** |
| `gap:` (+`column-gap`) | 115 + 1 | **0** |
| `grid-column/row`, `justify-items` | 15 | **0** |
| `outline` | 19 | **0** |
| `filter:` (CSS) | 2 | **0** |

Вёрстка при замене не изменилась: станция 640×900 / 1024×768 / 1280×800 / 1920×1080 — 0 перекрытий, 0 вне окна,
`minBtn` 32; кольцо станции `5×36 + 4×5 = 200`; сетка слотов `120×78`; низ репутации = низ панели (0 px);
строки заданий 44/48 px; календарь 12×30 = 360 клеток, 7 в строке, outside 0; док 96/187/278/324 px;
половины дока 229/229 и 370/370.
