# Баг: Визуал мезиевых сопел не виден в Play Mode

**Сессия:** 5 | **Дата:** 12 апреля 2026 | **Приоритет:** P2
**Статус:** ✅ Исправлен (авто-создание)

## Описание
МеziyThrusterVisual настроен в Inspector (ParticleSystem + Directional Light),
но в Play Mode при активации мезиевых модулей визуал не отображался.

## Причина
ParticleSystem нужно было вручную создать и назначить в Inspector.
У пользователя не было возможности добавить его — компонент отсутствовал.

## Исправление
Добавлен `AutoCreateParticles()` — автоматическое создание:
1. Дочерний объект "MeziyThruster" (позиция сопла — сзади корабля)
2. ParticleSystem с настроенным emission, shape, цветом (оранжевое пламя)
3. Point Light для свечения

**Срабатывает:**
- Автоматически при первом `Activate()` (если thrustParticle == null)
- Через кнопку в Inspector: **"Auto-Create Thruster Particles"**

## Затронутые файлы
- `Assets/_Project/Scripts/Ship/MeziyThrusterVisual.cs` — AutoCreateParticles(), CustomEditor с кнопкой
