## Qwen Added Memories
- Пользователь НЕ хочет, чтобы я спрашивал "применять ли изменения?" — нужно сразу применять исправления. Пользователь только тестирует результат в Unity и сообщает о проблемах. Он не разбирается в коде, моя задача — принимать технические решения самостоятельно.
- Текущая сессия посвящена работе с docs/roadmap.html — динамическая загрузка из MMO_Development_Plan.md, парсинг через marked.js + кастомный парсер задач, GitHub API коммиты. Ветка: qwen-gamestudio-agent-dev.
- КРИТИЧНО: Unity 6 URP настройка. НЕЛЬЗЯ создавать URP ассеты через C# скрипты — API отличается. Правильный путь: 1) Правой кнопкой в Project окне → Create → Rendering → URP Pipeline Asset → 2) Edit → Project Settings → Graphics → назначить Pipeline Asset → 3) Edit → Render Pipeline → URP → Upgrade Project Materials to URP Materials. ForwardRendererData переименован в UniversalRendererData (URP 14+). ScriptableRenderer не является UnityEngine.Object. rendererDataList — readonly. Standard шейдер → Universal Render Pipeline/Lit. CloudGhibli.shader использует URP Unlit includes.

## Agents & Skills Configuration

Все агенты и навыки находятся в папке `.agents/`:

```
.agents/
├── agents/           # 39 специализированных агентов
├── skills/           # 37 навыков (пошаговые инструкции)
├── rules/            # 11 стандартов кода
├── docs/             # Справочная документация
├── README.md         # Полный каталог агентов и навыков
├── PROJECT_C_AGENT_GUIDE.md  # Какие агенты для каких задач Project C
└── SKILLS_USAGE_GUIDE.md     # Как использовать навыки
```

### Как вызывать агентов

Напишите `@имя-агента` с задачей:
```
@unity-specialist "Архитектура системы инвентаря"
@network-programmer "Настрой NGO синхронизацию"
@technical-artist "Настрой CloudGhibli шейдер"
```

### Как использовать навыки

Напишите название навыка в запросе:
```
Проведи brainstorm для "система погоды"
Сделай code-review для Assets/_Project/Scripts/Network/
Спланируй sprint-plan
```

**Полный каталог:** [`.agents/README.md`](.agents/README.md)
