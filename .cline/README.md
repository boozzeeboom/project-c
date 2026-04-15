# Cline + MiniMax — Agent Configuration for Project C

**Адаптированная версия** из Opencode Game Studios для использования с Cline + MiniMax API.

## Структура

```
.cline/
├── clinerules.json      # Конфигурация агентов, хуков и разрешений
├── hooks/               # Shell-хуки для событий сессии
│   ├── session-start.sh
│   ├── session-stop.sh
│   ├── detect-gaps.sh
│   ├── validate-commit.sh
│   ├── validate-push.sh
│   ├── validate-assets.sh
│   └── pre-compact.sh
├── README.md            # Этот файл
└── CLAUDE.md            # Главный системный промпт

.clinerules/
├── agents/              # Определения агентов (39 штук)
├── skills/              # Навыки (37 штук)
├── rules/               # Правила кодирования (11 штук)
└── docs/                # Справочная документация
```

## Использование

### Вызов агента

```
@unity-specialist "Сделай архитектуру системы инвентаря"
@game-designer "Спроектируй систему крафта"
@network-programmer "Настрой NGO синхронизацию"
```

### Использование навыка

```
/brainstorm "система погоды"
/sprint-plan для UI системы
/code-review для Assets/_Project/Scripts/Network/
```

## Протокол сотрудничества

Каждый агент следует принципу:

```
Вопрос → Варианты → Решение → Черновик → Утверждение
```

- Агент **спрашивает** перед записью файлов
- Агент **показывает черновики** перед запросом одобрения
- Агент **не принимает решений** за вас — даёт экспертизу, вы решаете

## Критичные правила для Project C

### URP (КРИТИЧНО)
- ❌ НЕ создавать URP ассеты через C#
- ✅ ТОЛЬКО через Unity Editor UI
- ✅ Pipeline Asset → Edit → Project Settings → Graphics
- ✅ UniversalRendererData (НЕ ForwardRendererData)

### Git Workflow
- ✅ Ветка: основная рабочая ветка
- ❌ НЕ пушить в protected ветки без разрешения
- ✅ Коммитить часто, маленькими изменениями

## Collaboration
- ✅ Пользователь принимает ВСЕ решения
- ✅ Показывать черновики перед записью
- ✅ Спрашивать "Могу ли я записать в [filepath]?"
- ❌ НЕ коммитить без инструкции
- ❌ НЕ спрашивать "применять ли изменения?" — сразу применять

---

**Источник:** адаптировано из [Opencode Game Studios](https://github.com/boozzeeboom/opencode-gamestudio)  
**Адаптация:** для Cline + MiniMax  
**Версия:** 1.0.0