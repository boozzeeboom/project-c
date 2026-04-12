# Баг: Визуал мезиевых сопел не виден в Play Mode

**Сессия:** 5 → 5_2 | **Дата:** 12 апреля 2026 | **Приоритет:** P1
**Статус:** ✅ Исправлен (дополнительная защита в сессии 5_2)

## Описание
MeziyThrusterVisual настроен в Inspector (ParticleSystem + Directional Light),
но в Play Mode при активации мезиевых модулей визуал не отображался.

## Причина (сессия 5)
ParticleSystem нужно было вручную создать и назначить в Inspector.
У пользователя не было возможности добавить его — компонент отсутствовал.

## Исправление (сессия 5)
Добавлен `AutoCreateParticles()` — автоматическое создание:
1. Дочерний объект "MeziyThruster" (позиция сопла — сзади корабля)
2. ParticleSystem с настроенным emission, shape, цветом (оранжевое пламя)
3. Point Light для свечения

## Исправление (сессия 5_2)
Добавлен `Awake()` + `EnsureDeactivated()` — гарантия что частицы **выключены** при старте.
Защита от старых объектов в сцене из прошлых тестов.

**Срабатывает:**
- Автоматически при первом `Activate()` (если thrustParticle == null)
- Через кнопку в Inspector: **"Auto-Create Thruster Particles"**
- **Awake():** принудительно выключает все частицы

## Затронутые файлы
- `Assets/_Project/Scripts/Ship/MeziyThrusterVisual.cs` — AutoCreateParticles(), CustomEditor (сессия 5)
- `Assets/_Project/Scripts/Ship/MeziyThrusterVisual.cs` — Awake() + EnsureDeactivated() (сессия 5_2)
