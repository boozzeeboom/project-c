# 02 — Модульная одежда: слои, каталог модулей, промпты первичной генерации

**Назначение:** превратить концепт-дек женского пилота в **модульную систему одежды** и дать готовые **описания (промпты)** для первичной генерации изображений — чистых, без фона, пригодных для image→3D.
**Обновлено:** 2026-10-25.
**Опора:** концепт-дек `Female_merged_full_pdf_02.pdf` (126 стр., только изображения, текстового слоя нет; рабочий рисунок костюма — 10 листов), `ART_STYLE_MANIFEST.md` §2, §5, §7, §10, `GDD_11_Inventory_Items.md` §2 (`EquipSlot`).

---

## 1. Что реально даёт концепт-дек

Файл `Female_merged_full_pdf_02.pdf` — 126 страниц, по одному изображению на страницу, **текста нет** (инвентарь страниц сохранён в `_concept_female/_pdf_text.txt`). Содержательная часть по костюму — 10 листов в `...\female_concept_1\edited_ru\01..10.jpg` (разбор сделан по временным копиям; сами копии в репозитории не хранятся).

| Лист | Что на нём | Что берём в модульную систему |
|---|---|---|
| 01 | «Female Pilot · Project C: The Clouds · концепт дизайн»; портрет, поясные дуги, **три золотые точки** на воротнике/плече | золотые точки = точки нагрузки → в 3D это `socket` крепления модулей |
| 02 | два варианта одного комбинезона (чёрный и серо-стальной): молния от горла до пояса, высокий воротник, наплечник, наруч, тактические ремни, знак на груди; **5 цветовых полос** | база = **один меш + 5 тканевых окрасов**; вариантность покраской, а не новыми мешами |
| 03 | разнесённый стек: аксессуары → стальные пластины → ремни → сетка; жилет из сегментов, садящийся на «шипы» ремней | **4 уровня слоёв**: L0 сетка → L1 стропа → L2 пластины → L3 аксессуары |
| 04 | примерка на 4 типах фигур (ребёнок / подросток / атлет / плюс), контуры обхватов, регулировочные пряжки | модуль обязан садиться на разные обхваты — за счёт регулировки, а не перекроя |
| 05 | полный фронт + вид сзади, риски `159` / `157`, маркировка **«3‑2‑3»**, центральный `cyan`-элемент на груди | риски — вспомогательные (в 3D не переносятся); `cyan` = антигравий, см. §1.2 |
| 06 | три рисунка сетки (ромб / «кирпич» / соты), 5 цветов, макро-плетение, манжета | сетка = **отдельный модуль**, рисунок — вариант, не новый меш |
| 07 | фиолетовый костюм + короткая куртка, кобура с инструментом, высокие сапоги, линейные варианты обвязки | гражданско-гильдейский слой поверх тактической базы |
| 08 | **система крепления**: «паук» из 8 строп, крюки и кольца, пластины защёлкиваются на крюки, схема «снять → повесить → надеть» | ядро модульности: пластины крепятся **к обвязке**, не к телу |
| 09 | шлем: 3 состояния визора (прозрачный → янтарный → глухой + красная точка опасности) | шлем = один меш + 3 варианта визора |
| 10 | 5 силуэтов причёсок и фиксация хвоста под шлемом | шлем должен вмещать все 5 силуэтов (объём сверху и сзади) |

### 1.2. Три конфликта концепта с лором и решения

1. **Голубое свечение на груди и голубые «крюки-прицелы» (листы 05, 08).** По манифесту §2.1 и §5 голубой `#4fc3f7` — это **антигравий**, а не интерфейс. → центральный элемент груди трактуем как **антигравийный узел** (`CL_F_BACK_RIG_01` / ядро груди), а cyan-«прицелы» листа 08 в арт одежды **не переносим** — это UI/инфографика.
2. **Фиолетовый костюм и малиновые ремни (листы 05, 07).** Цвета гильдий допустимы **только на ткани, эмали знаков и флагах** (манифест §2.2). Фиолетовая ткань = Гильдия Тайн; малиновая стропа — **окрашенный текстиль**, не свечение.
3. **Красная точка на визоре (лист 09).** В грамматике свечения красного нет (три смысла: cyan / green / violet). → допустима только как **крошечный аварийный индикатор прибора**, и это единственное место; на костюмах красного свечения нет. Требует утверждения (§10).

---

## 2. Слои и слоты

Порядок надевания (нижний → верхний): `L0 база` → `L1 обвязка` → `L2 пластины` → `L3 аксессуары` → `OUT верхняя одежда`.

| Слой | Слот `EquipSlot` (`GDD_11` §2) | Модули |
|---|---|---|
| L0 база | `Chest` 2, `Legs` 3 | `CL_F_CHEST_UNSUIT_01`, `CL_F_LEGS_UNSUIT_01` |
| L1 обвязка | `Chest` 2 | `CL_F_CHEST_HARNESS_01`, `CL_F_LEGS_BELT_01` |
| L2 пластины | `Chest` 2 | `CL_F_CHEST_PLATE_01` |
| L2 пластины | `Accessory1/2` 7/8 | `CL_F_ACC_PAULDRON_01` (×2 — левое/правое) |
| L2 пластины | `Hands` 6, `Legs` 3 | `CL_F_HANDS_BRACER_01`, `CL_F_LEGS_KNEE_01` |
| L3 аксессуары | `Hands` 6, `Feet` 4, `Head` 1, `Back` 5 | `CL_F_HANDS_GLOVE_01`, `CL_F_FEET_BOOT_01/02`, `CL_F_HEAD_HELMET_01`, `CL_F_BACK_RIG_01` |
| L3 мелочь | `Accessory1/2` 7/8 | `CL_F_ACC_POUCH_01`, `CL_F_ACC_TOOLROLL_01`, `CL_F_ACC_BADGE_01`, `CL_F_LEGS_HOLSTER_01` |
| OUT | `Chest` 2 / `Legs` 3 | `OUT_JACKET_CROP_01`, `OUT_APRON_CANVAS_01`, `OUT_COAT_WOOL_01` |

`Module1/2/3` (20–22) — **импланты**, не одежда (см. `Battle/02_LORE.md` §3): они видимы, но в этой системе не участвуют.

**Правило совместимости:** пластина надевается только на обвязку (нужны крюки), обвязка — только на базу (нужны точки под нагрузку); наруч и наколенник держатся на собственных ремнях и работают без обвязки.

---

## 3. Каталог модулей

| Код | Слот | Это | Лор-опора |
|---|---|---|---|
| `CL_F_CHEST_UNSUIT_01` | Chest | сетчатый комбинезон-боди: высокий воротник, молния, длинные рукава из сетки | лист 02/06; `Fabric` (сетка, ворс, затяжки) |
| `CL_F_LEGS_UNSUIT_01` | Legs | лосины той же вязки, сетчатые вставки на бёдрах | лист 02/06 |
| `CL_F_CHEST_HARNESS_01` | Chest | «паук»: 8 строп от нагрудного кольца, крюки на концах, широкий пояс | лист 03/05/08 |
| `CL_F_CHEST_PLATE_01` | Chest | сегментный нагрудник: 2 чашки + стерновая пластина + 5 ламелей | лист 03 |
| `CL_F_ACC_PAULDRON_01` | Accessory1/2 | наплечник из 3 ламелей с кольцом на внутренней стороне | лист 02/05/08 |
| `CL_F_HANDS_BRACER_01` | Hands | наруч на 2 ламелях, шарнир, кожаная подкладка | лист 02/05 |
| `CL_F_HANDS_GLOVE_01` | Hands | беспалый полётный перчат с сетчатым тылом | лист 02/05/07 |
| `CL_F_LEGS_BELT_01` | Legs | широкий пояс, пряжка-пластина, люверсы, петли под инструмент | лист 03/05/08 |
| `CL_F_LEGS_HOLSTER_01` | Legs (Accessory) | набедренная кобура под инструмент, 2 стропы с пряжками | лист 07 |
| `CL_F_LEGS_KNEE_01` | Legs | наколенник-нашлёпка на коже и 3 заклёпках | лист 08 |
| `CL_F_FEET_BOOT_01` | Feet | ботинок до середины голени, стальной носок, шнуровка | лист 02 |
| `CL_F_FEET_BOOT_02` | Feet | высокий сапог с цветной окантовкой | лист 07 |
| `CL_F_HEAD_HELMET_01` | Head | шлем с петлевым визором, 3 варианта визора | лист 09 |
| `CL_F_BACK_RIG_01` | Back | спинная рама-стабилизатор: 2 кольца антигравия, манометр, картридж-гнездо | манифест §5 (антигравий = кольца/диски/рамки-контуры) — **дизайн-изобретение**, см. §10 |
| `CL_F_ACC_POUCH_01` | Accessory1/2 | плоская сумка-планшет с клапаном | лист 03 |
| `CL_F_ACC_TOOLROLL_01` | Accessory1/2 | холщовый рулет под инструмент | лист 03/07 |
| `CL_F_ACC_BADGE_01` | Accessory1/2 | эмалевый знак (гильдия / СОЛ), ранг — полоски | манифест §7 |
| `OUT_JACKET_CROP_01` | Chest | короткая куртка с нашивкой | лист 07 |
| `OUT_APRON_CANVAS_01` | Legs | холщовый фартук фермера с заплатами | манифест §7 (фермеры) |
| `OUT_COAT_WOOL_01` | Chest | плотное шерстяное пальто богатого горожанина | манифест §7 (богатые) |

---

## 4. Требования к изображению под image→3D

Это **не** нарративный арт. Задача картинки — чтобы 3D-модель построилась по силуэту.

| Параметр | Требование |
|---|---|
| Формат | PNG, 2048×2048 (минимум 1024), предметы — 1:1; мягкие вещи — 3:4 |
| Фон | чистый белый `#ffffff` **или** альфа-канал; ничего больше в кадре |
| Проекция | ортографическая, фронт; камера **перпендикулярно** предмету, без перспективы |
| Кадр | предмет по центру, занимает 78–85 % кадра, силуэт целиком, поля ровные |
| Свет | ровный студийный (key сверху-слева, fill справа, мягкий верх); **без** контрового света, без блик-блома |
| Тени | нет падающей тени, нет контактной тени, нет пола/подставки, нет отражений |
| AO | без запечённого AO и «грязной» виньетки — 3D должно получить чистую форму |
| Аннотации | нет текста, цифр, стрелок, выносок, размерных линий, сеток, водяных знаков |
| Мягкие вещи | на «призрачном манекене»: матовый серый `#9a9a9a` торс/нога без головы и рук, обрезанный чисто |
| Твёрдые пластины | отдельный объект «в воздухе», без тела |
| Материал | читается **цветовыми зонами и износом**, а не фототекстурой: в игре 95 % поверхностей процедурные (манифест §6) |
| Один файл | один модуль; не делаем коллаж из нескольких предметов в одном файле |
| Набор | все файлы набора снимаются одинаковым кадром (одна «камера», одна дистанция) |

`НЕ ДЛЯ 3D`: лист из трёх видов на одной картинке. Он годится только для согласования дизайна, для реконструкции его не используем.

---

## 5. Промпт-блоки

Рецепт сборки: **`[STYLE]` + `[SUBJECT модуля]` + `[PRODUCT]` + `[NEGATIVE]`**.

### 5.1 `[STYLE]` — общий (обязателен в каждом промпте)

```
STYLE: stylized sci-fi graphic-novel production painting, western comic-book look, clean soft dark contour,
simplified readable volumes, flat color zones with soft gradients, material described by form and wear rather than
photographic texture; industrial steampunk-dieselpunk hardware: riveted steel, welded seams, brass fittings,
oxidized green patina, rust streaks, woven webbing, canvas, leather; palette anchors deep navy #1a1a2e,
gunmetal #4a4a4a, rust #8b5e3c, patina #5a7a6a, brass, bone-white #f5f0e8; the only emitted light is functional
glow: cyan #4fc3f7 antigravity, green #66bb6a mesium (dangerous), violet #b366ff the Veil; no magic, every part
functional and bolted on.
```

### 5.2 `[PRODUCT]` — два варианта

**A. Твёрдый предмет / пластина (в воздухе):**
```
PRODUCT SHOT: single isolated object, nothing else in frame, orthographic front view, camera perpendicular to the
object, no perspective distortion, object centered filling 80% of the frame, entire silhouette inside the frame
with even margins, even three-point studio light (soft key upper-left, fill right, soft top), no rim light,
no hard highlights, no cast shadow, no contact shadow, no ground plane, no reflection, no ambient occlusion
darkening, pure white background #ffffff, no border, hi-res clean edges.
```

**B. Мягкая вещь (на призрачном манекене):**
```
PRODUCT SHOT: garment shown as if worn on an invisible ghost mannequin, neutral matte grey #9a9a9a mannequin form
only, cleanly cropped at the neck and at the hip or knee, no head, no hands, no skin, no face, orthographic front
view, camera perpendicular, no perspective distortion, garment centered filling 85% of the frame, entire
silhouette inside the frame, even three-point studio light, no rim light, no cast shadow, no contact shadow,
no ground plane, no reflection, no ambient occlusion darkening, pure white background #ffffff, no border,
hi-res clean edges.
```

### 5.3 `[NEGATIVE]` — общий (обязателен)

```
photorealism, photograph of real fabric, cinematic scene, background scenery, environment, props, hands, arms,
head, face, visible person, extra limbs, mannequin head, ground plane, floor, drop shadow, hard specular
highlight, bloom, lens flare, bokeh, depth of field, perspective distortion, foreshortening, three-quarter pose,
tilted camera, cropped object, cut off silhouette, collage, multiple objects, duplicated object, text, caption,
labels, numbers, arrows, callout lines, dimension lines, measurement marks, grid overlay, watermark, signature,
logo, UI overlay, neon glow, cyberpunk neon, glowing runes, magic, chrome cybernetic implants, white plastic
hard-surface sci-fi, space armour, medieval fantasy armour, excessive spikes, anime style, chibi proportions,
flat vector art, muddy colors, JPEG artifacts
```

---

## 6. Описания по модулям

Ниже — только `SUBJECT / DETAIL / MATERIAL`. Перед запуском подставить `[STYLE]`, после — `[PRODUCT A или B]` и `[NEGATIVE]`. В скобках указан тип `PRODUCT`.

### 6.1. База

**`CL_F_CHEST_UNSUIT_01`** — `PRODUCT B`
```
SUBJECT: sleeveless fitted bodysuit bodice of a female pilot: high stand-up collar with a folded top edge,
a full-length front zipper running from the throat down past the waist, long snug mesh sleeves joined at the
shoulders, two clean bust seams, close-fitting waist, no prints, no logos.
DETAIL: fine diamond fishnet knit panels across the chest and ribs, denser matte knit on the abdomen and collar,
flat-felled seam lines, a short leather zip pull, small reinforced load patches at both clavicles where harness
rings will sit, a narrow strip of lighter knit at the collar top.
MATERIAL: dark gunmetal-grey #4a4a4a technical knit with a faint sheen, faded and slightly slick at the elbows and
collar, two pulled threads near a mesh panel edge, no printed pattern.
```

**`CL_F_LEGS_UNSUIT_01`** — `PRODUCT B`
```
SUBJECT: snug leggings of the same pilot suit, wide flat waistband, no pockets, no belt, ankle-length.
DETAIL: mesh cut-out panels on the outer thighs, darts at the knees, reinforced seat, flat-felled seams,
a short zipper gusset at the front of the waistband.
MATERIAL: dark gunmetal-grey technical knit, worn gloss at the inner thighs and knee tops, one snag with a loose
thread on the right thigh, faint dust at the hems.
```

### 6.2. Обвязка

**`CL_F_CHEST_HARNESS_01`** — `PRODUCT B`
```
SUBJECT: eight-arm load-bearing webbing harness for a female torso: one steel ring at the centre of the sternum,
eight dyed webbing straps radiating from it — two over the shoulders, two under the arms, two across the ribs,
two down to a wide waist belt — each strap ending in a steel carabiner hook with a screwed-on ring; the waist belt
is wide with a rectangular steel buckle and two rows of brass eyelets.
DETAIL: doubled stitching along every strap, blued steel plates riveted where the straps cross on the back,
adjustable slide buckles on four straps, frayed webbing ends, a few snapped threads, small leather reinforcement
patches at each hook, the chest ring polished bright by hands.
MATERIAL: dyed magenta-crimson webbing, matte textile (no glow), blued steel hardware, brass rivets, worn edges
and whitened fold creases.
```

**`CL_F_LEGS_BELT_01`** — `PRODUCT B`
```
SUBJECT: wide utility belt: rectangular steel buckle plate with a forged tongue, two rows of brass eyelets,
doubled leather tongue, four leather tool loops, one small brass D-ring, two hanging strap ends.
DETAIL: heavy edge stitching, punched and stretched holes, one loop repaired with wire, scratches across the
buckle plate, the leather cracked along the fold line.
MATERIAL: oiled black-brown leather, worn steel buckle, brass eyelets with greenish patina.
```

### 6.3. Пластины

**`CL_F_CHEST_PLATE_01`** — `PRODUCT A`
```
SUBJECT: segmented steel breastplate for a female torso, built from five pieces: two domed breast cups with rolled
rims, a narrow sternum plate between them, and five stacked abdominal lames below arranged in a shallow V; on the
inner face four steel bosses with cotter pins engage harness hooks; no shoulder straps and no buckles — the whole
piece is a single clip-on shell.
DETAIL: planished hammered steel, brass rivets along each lame edge, hand-rolled edges, chain-riveted leather
strips on the inner face, small dents and a scratch cluster on the left cup, soot in the lame gaps.
MATERIAL: worn polished steel #6a6a6a with a cold bluish sheen, rust freckles in the rivet lines, patina darkening
in the creases.
```

**`CL_F_ACC_PAULDRON_01`** — `PRODUCT A`
```
SUBJECT: layered steel shoulder pauldron, three overlapping lames covering the deltoid, the top lame raised with a
rolled outer edge, a steel ring and hook on the inner face, two leather tabs and a cotter pin.
DETAIL: planished surface, one brass rivet per lame on the centre line, riveted leather pad inside, deep gouge on
the outer edge, three shallow dents.
MATERIAL: worn steel, brass rivets, oiled black-brown leather, faint rust bloom along the lowest lame.
```

**`CL_F_HANDS_BRACER_01`** — `PRODUCT A`
```
SUBJECT: forearm bracer made of two overlapping steel lames covering the outer forearm from wrist to mid-forearm,
hinged on the inner side, mounted on a leather backing cuff, one small brass buckle with a dyed webbing strap.
DETAIL: rolled edges, four brass rivets, hinge pin visible on the inner side, planished surface with a worn
bright band where it rubs the suit.
MATERIAL: worn steel with a cold sheen, brass buckle, oiled leather backing, thin rust lines in the hinge.
```

**`CL_F_LEGS_KNEE_01`** — `PRODUCT A`
```
SUBJECT: single clip-on knee guard: a domed steel cap over a leather backing, three brass rivets, a webbing strap
with a steel snap passing behind the knee.
DETAIL: planished dome, rolled rim, one deep scrape across the crown, strap end seared and frayed.
MATERIAL: worn steel, brass rivets, oiled leather, dyed webbing.
```

### 6.4. Конечности и ступни

**`CL_F_HANDS_GLOVE_01`** — `PRODUCT A`
```
SUBJECT: single fingerless flight glove, right hand, laid out flat with fingers spread; mesh knit back panel,
oiled leather palm with a reinforced patch, a knuckle guard of three small leather pads, a dyed webbing wrist
strap with a steel snap, exposed fingers and thumb tip.
DETAIL: double stitching at the finger openings, worn leather at the grip, mesh showing a repair stitch,
strap end frayed.
MATERIAL: dark leather and gunmetal-grey mesh, steel snap, dyed webbing, oil darkening the palm.
```

**`CL_F_FEET_BOOT_01`** — `PRODUCT A`
```
SUBJECT: single mid-calf flight boot, shown floating in profile with the toe pointing to the left:
eight brass eyelets with dark laces, steel toe cap riveted on, a dyed webbing strap with a square buckle across
the instep, stacked leather heel, thick treaded sole.
DETAIL: heavy welt stitching, scuffed toe cap, cracked leather at the ankle flex, mud crusted on the sole edge,
lace end burned.
MATERIAL: black-brown oiled leather, worn steel toe cap, brass eyelets, dark rubber-look sole.
```

**`CL_F_FEET_BOOT_02`** — `PRODUCT A`
```
SUBJECT: single tall thigh-high boot shown floating in profile, toe pointing left: smooth shaft up to the thigh,
a narrow contrasting trim band at the top, plain pointed toe, low stacked heel, a short inner zip.
DETAIL: unmarked shaft with a soft fold above the ankle, stitched trim band, a small scuff cluster at the toe,
subtle sheen along the shin.
MATERIAL: dyed violet suede-like shaft (textile dye, not glowing) with darker leather foot, matte finish.
```

### 6.5. Голова и спина

**`CL_F_HEAD_HELMET_01`** — `PRODUCT A`
```
SUBJECT: pilot flight helmet, empty (no head inside), shown from a slight front-left angle but with orthographic
flatness: fibre-glass dome shell with two shallow longitudinal ridges, a hinged bubble visor with a steel frame,
two round ear pods with knurled rims, a chin strap with a snap buckle hanging loose, a short stub of an oxygen hose
on the left side, steel hinge plates on both sides.
DETAIL: panel seams painted over, a chip in the paint at the crown exposing the shell, worn chin strap, scratched
visor with a smudge at the edge, a small enamel unit badge on the left temple.
MATERIAL: bone-white to grey painted shell #d4d0c8 with grime in the seams, dark steel hinges, clear visor glass.
VISOR VARIANTS (генерировать отдельными файлами): (a) clear glass; (b) amber-tinted glass;
(c) opaque dark glass with one tiny red hazard dot in the lower corner — the only red light on the whole set.
```

**`CL_F_BACK_RIG_01`** — `PRODUCT A`
```
SUBJECT: compact back-mounted antigravity stabilizer frame for a pilot (no wearer): a riveted steel spine plate
with two large brass ring assemblies on the upper back, each ring holding a machined drum with fine slots; a small
pressure gauge with a white dial and a bent needle between the rings; two braided hoses running down to a cartridge
socket at the lower back; a leather shoulder yoke and a wide waist strap.
DETAIL: welded seams, brass fittings, knurled adjustments, a patched hose, soot around the cartridge socket, one
strand of wire re-twisted by hand.
MATERIAL: gunmetal steel and oxidized brass, oiled leather yoke, and ONLY the inside of the two drums emitting a
soft cyan #4fc3f7 antigravity light — no other glow anywhere.
```

### 6.6. Аксессуары

**`CL_F_LEGS_HOLSTER_01`** — `PRODUCT A`
```
SUBJECT: right-thigh tool holster: rectangular stiffened pocket in canvas and leather with a steel frame inside,
a flap with a brass snap, two dyed webbing straps with cam buckles for the thigh, a small wrench held in a leather
loop at the top edge.
DETAIL: box stitching on the frame corners, worn canvas at the flap edge, brass snap polished, straps creased at
the buckles, wrench with worn chrome and a taped grip.
MATERIAL: sand-grey canvas, oiled leather, dyed webbing, blued steel frame, brass snap.
```

**`CL_F_ACC_POUCH_01`** — `PRODUCT A`
```
SUBJECT: flat satchel pouch, rectangular with rounded corners, a single flap covering two thirds of the front,
one brass snap and a short leather keeper, a dyed webbing belt loop on the back.
DETAIL: double stitching, one patched tear on the back panel, the flap edge softened and furred, faint dirt at the
bottom corners.
MATERIAL: dark olive canvas, oiled leather flap, brass snap, dyed webbing.
```

**`CL_F_ACC_TOOLROLL_01`** — `PRODUCT A`
```
SUBJECT: canvas tool roll, unrolled flat, twelve stitched pockets holding four hand tools — a spanner, a flat
chisel, a file and a brass calliper — two leather ties and a riveted strap.
DETAIL: heavy hem stitching, oil stains around each pocket, one pocket repaired with a hand-cut patch, tools with
worn chrome and taped handles.
MATERIAL: sand-grey canvas, oiled leather ties, brass rivets, worn steel tools.
```

**`CL_F_ACC_BADGE_01`** — `PRODUCT A`
```
SUBJECT: small stamped metal unit badge, shield shape with a knurled rim, a coloured enamel field with a simple
industrial emblem — a gear crossed by a spanner — and three narrow rank stripes on a lower band, a pin and clasp
on the back.
DETAIL: enamel slightly chipped at one corner, brass rim polished at the high points, stripes hand-painted with
uneven edges.
MATERIAL: brass body, enamel field (guild colour as fabric-grade paint, never glowing), steel pin.
```

### 6.7. Верхняя одежда (социальные слои)

**`OUT_JACKET_CROP_01`** — `PRODUCT B`
```
SUBJECT: cropped work jacket, waist length, standing collar, front zip with a leather pull, two chest pocket
flaps, a rectangular unit patch on the right shoulder, worn cuffs.
DETAIL: sleeve seams set in, elbow patches, hem stitched with a double row, patch sewn by hand with visible
uneven thread, collar edge furred.
MATERIAL: dark grey-green heavy twill with faded shoulders, brass zip, leather pull, wool patch.
```

**`OUT_APRON_CANVAS_01`** — `PRODUCT B`
```
SUBJECT: farmer work apron, bib and skirt cut as one piece, crossed back straps, one large front pocket split in
two, three cloth patches, a rope tie at the waist.
DETAIL: hardened creases at the knees, soil stains along the hem, patches of three different fabrics stitched by
hand, one strap re-joined with a knot.
MATERIAL: coarse unbleached canvas, straw-dust in the fibres, sisal rope tie, dull steel buttons.
```

**`OUT_COAT_WOOL_01`** — `PRODUCT B`
```
SUBJECT: long heavy town coat, knee length, high notched collar, double-breasted front with six brass buttons,
two welt pockets, a half belt at the back, straight sleeves with turned cuffs.
DETAIL: perfectly even seams, hand-finished buttonholes, one button replaced with a plainer one, subtle sheen at
the collar and cuffs, lining visible at the vent.
MATERIAL: dense charcoal wool, brass buttons, dark silk lining — richer material and cleaner finish than any
work garment, no decoration for its own sake.
```

---

## 7. Вариантность покраской, а не новыми мешами

Один меш → N материалов из существующих семейств (`Fabric`, `Plate`, `Bronze`, `IronRust`). Никаких новых текстур.

| Слой / фракция | Базовая ткань | Стропа | Пластины | Эмаль знака | Свечение |
|---|---|---|---|---|---|
| Пилот (база дока) | gunmetal `#4a4a4a` | малиново-красная | сталь | cyan-эмаль | только антигравийные кольца |
| Гильдия Мысли | оранжевая ткань | тёмно-коричневая | сталь | оранжевая эмаль | — |
| Гильдия Созидания | зелёная ткань | тёмная | сталь | зелёная эмаль | — |
| Гильдия Силы | красная ткань | чёрная | сталь, наплечник тяжелее | красная эмаль | — |
| Гильдия Тайн | фиолетовая ткань | малиновая | сталь | фиолетовая эмаль | — |
| Гильдия Успеха | жёлтая ткань | тёмная | сталь | жёлтая эмаль | — |
| Новое Правительство (СОЛ) | чёрная ткань, глухой воротник, глянец | чёрная | сталь, без наколенников | бронза, знак СОЛ | — (сканер в руке, не на костюме) |
| Богатый горожанин | плотное шерстяное сукно | — | — | бронза | — |
| Фермер | холст, заплаты | верёвка | — | — | — |
| Потеряшка / алиенум | рваная, непарная | — | ржавые обломки | — | — |
| Бандит | кожа, тряпки | сыромятная | ржавое железо | — | — |
| Ржавый пират | кожа | — | ржавые пластины, крюки | — | — |

---

## 8. После генерации: 3D → Unity

- **Имена:** `SM_CL_F_<SLOT>_<FAMILY>_<NN>` (например `SM_CL_F_CHEST_PLATE_01`); материал — `M_PC_<Family>_<Color>`.
- **Масштаб:** метры. Задаётся по ростовой сетке манекена, не по картинке.
- **Ориджин:** в точке крепления (`socket` на обвязке / кость), а не в центре габарита — иначе посадка будет «плавать».
- **Поза:** меш строится в T-позе; мягкие вещи — по телу-референсу T-позы, пластины — в рабочем положении.
- **Бюджет:** база ≤ 3 000 тр., пластина ≤ 800 тр., аксессуар ≤ 500 тр., шлем ≤ 2 500 тр.
- **Материалы:** только процедурные семейства проекта; назначение через `sharedMaterials`, никогда через `.material`.
- **Сокеты:** имена креплений = имена в `EquipmentVisualSocket` (кость + смещение), чтобы пауldron/кобура/рама садились без ручного подгона.
- **Совместимость:** набор должен собираться в любую комбинацию L0…L3 без пересечений; проверять численно (пересечение AABB + фактические вершины), а не глазами.

---

## 9. Чек-лист приёмки изображения (до отправки в 3D)

- [ ] Фон чистый белый или прозрачный, в кадре нет ничего лишнего.
- [ ] Предмет целиком в кадре, поля ровные, силуэт не обрезан.
- [ ] Проекция ортографическая: нет ракурса 3/4, нет перспективного сужения.
- [ ] Нет падающей и контактной тени, нет пола и отражений.
- [ ] Нет текста, стрелок, выносок, размерных линий, сеток и водяных знаков.
- [ ] Один предмет на файл.
- [ ] Материал читается цветом и износом, а не фототекстурой.
- [ ] Свечение только там, где оно по лору (cyan / green / violet), и не больше одной зоны.
- [ ] Цвета гильдий — только ткань и эмаль, не техно-эмиссия.
- [ ] Все файлы набора сняты одинаковым кадром.

---

## 10. К утверждению с заказчиком

1. `CL_F_BACK_RIG_01` — персональная антигравийная рама-стабилизатор. В документах личного антигравийного снаряжения пилота **нет**; предложено по грамматике «антигравий = кольца и рамки-контуры» (манифест §5). Нужно подтверждение.
2. Красная точка на глухом визоре — единственный красный источник в наборе; требует утверждения как аварийный индикатор.
3. Риски `159` / `157` и маркировка «3‑2‑3» на листе 05 — предположительно габариты в см и деление корпуса; для 3D не используются, но трактовку стоит подтвердить.
4. Наплечники вынесены в `Accessory1/2`, а не в `Chest` — так их можно менять по одному. Подтвердить.
5. Нужен ли отдельный форменный вариант СОЛ на тех же мешах (чёрная ткань, бронза, без наколенников) или это другой комплект.
6. Три состояния визора: отдельные меши или один меш + 3 материала визора (предложено второе).
