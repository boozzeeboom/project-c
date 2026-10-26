# Стойка и силуэт для референсных изображений персонажей

> **Пропорции здесь намеренно не регламентируются.** Любые правила вида «фигура ровно N голов»,
> ориентиры в долях роста и требования «широкая и короткая фигура» уводили генерацию в
> карликовые и детские пропорции (`Winter_Scholar_*_v02`–`v07`, `Adjudicator_*_v01`).
> Весь регламент пропорций удалён по требованию заказчика. В промптах персонажей не задавать
> ни число голов, ни доли роста, ни «wide and short», ни «head is visibly large».

## Стойка (A-pose) — рабочая формулировка

Проблема: генератор по умолчанию ставит женские фигуры со сведёнными ногами — бёдра и голени
слипаются, просвета нет, референс не годится ни для ретопологии, ни для чтения подвязок и
раздвижки ног в 3D. Слово «A-pose» в начале промпта этого не лечит.

Лечится отдельным блоком стойки впереди всех остальных требований, с указанием просвета в
единицах и списком запретов.

```
STANCE: she stands with her feet planted apart, the inner edges of her boots separated by a clear
gap of half a head-width, both legs held straight and fully separated, an open band of empty
background visible between the calves down to the floor, knees apart and not touching, toes
pointing forward and slightly outward, weight even on both feet, arms held away from the body at
roughly forty-five degrees so no arm touches the torso, hands open and relaxed.
FORBIDDEN STANCE: legs pressed together, knees touching, thighs touching, glued or closed legs,
feet together, narrow pigeon stance, crossed ankles, one leg hidden behind the other.
```

Проверено на `PC_Char_Winter_Command_Female_APOSE_v03` и
`PC_Char_Winter_Officer_Female_APOSE_v03` — просвет появился у обеих от паха до пола.

Наблюдение: просвет легко перелетает в обратную сторону (слишком широкая стойка, почти
«верхом»). Если нужна умеренная ширина, заменить `half a head-width` на `a narrow gap`.

Стойка обязательна и для мужских фигур: у них того же дефекта меньше, но просвет между
голенями всё равно теряется под широкими брюками.

## Длинная одежда и правило стойки

Длинный закрытый халат прячет и просвет между ногами, и подвязки. Разводить его в открытый
плащ нельзя — теряется силуэт запахнутого кимоно.

Рабочее решение: халат остаётся закрытым по всей длине, запах левой полы на правую, но в
передних полах идут две узкие прямые прорези от середины бедра вниз — через них видны бёдра,
обе подвязки и просвет.

## Женский hourglass — усиленный вариант

Первая версия hourglass проигрывалась: эполеты и меховые воротники расширяют плечи, и фигура
садится прямоугольником. Работает формулировка через «самая широкая точка тела»:

```
Strict hourglass silhouette: the hips are the widest point of the entire body and stay wider
than the shoulders even with the epaulettes on, the waist is deeply pinched and clearly narrower
than both the ribcage and the hips, with a visible waist-to-hip break rather than a straight
column.
```

Обратный перекос: при слишком жёсткой формулировке бёдра раздуваются. Держать через
`full but not heavy thighs, the width comes from the hip line and not from thick legs`.

## Разгрузка и подвязки — инвариант

Не менять в батчах: два широких оксблад-ремешка от пояса через плечи в стальное кольцо на
кожаной подушке на грудине, два узких подмышечных ремешка в то же кольцо, латунные пряжки с
свисающими концами, широкий пояс с люверсами и прямоугольной бляхой, ровно по одной узкой
подвязке на бедро.
