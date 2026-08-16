# Iterations

## Итерация от 15 августа 2026

**Задача:** Настройка первого квеста-гайда Onboarding alfa для проверки NPC, диалогов, квестовых стадий, локализации и выдачи ключа корабля.
**Коммит:** `f9eac76d7149ec33bd97c31fdb6a9bdc4cd1e275` — T-QST01: Добавлен первый квест-гайд Onboarding alfa
**Изменения:**
- Созданы QuestDefinition `onboarding_alfa`, NpcDefinition `OnboardingAlfa` и DialogTree `OnboardingAlfaDialog`.
- Onboarding alfa размещён на крыше `Ангар1 средняя часть.001` как экземпляр канонического NPC-префаба.
- Обновлён `MiraDefault`: маршрут через `RepairManager`, `MarketZone_Primium` и возвраты к Мира.
- Зарегистрированы новые NPC, диалог и квест в `QuestDatabase`.
- Добавлены русские и английские строки в `Dialogue_Table`.
- Исправлена выдача квестовых предметов с сохранением `ItemType`, включая `Key`.
- Серверный диалог теперь локализует текст реплик, имена NPC и варианты ответа.
- Проверка графов и компиляции пройдена без ошибок.

## Коррекция от 16 августа 2026

**Задача:** Перевести возвраты к перемещающейся Mira на `TalkToNpc` и обновить внутренний гайдлайн первого onboarding-квеста.
**Коммит:** `ddfa001c08bf184a4fa0df0e0f9817eb65f01e5b` — T-QST02: Исправлен возврат к Mira через TalkToNpc
**Изменения:**
- В `onboarding_alfa` этапы `return_from_repair` и `return_from_market` используют `TalkToNpc` с `mira_01` и `Mira.asset`.
- Статичные точки `RepairManager` и `MarketZone_Primium` оставлены на `ReachLocation`.
- Гайдлайн сохранён в `docs/dev/TESTS/first-auto-quest/README.md` и дополнен правилом выбора objective по типу цели.
- Статическая проверка Unity: `No compile errors`.
