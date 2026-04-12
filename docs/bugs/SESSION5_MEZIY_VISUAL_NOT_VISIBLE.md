# Баг: Визуал мезиевых сопел не виден в Play Mode

**Сессия:** 5 | **Дата:** 12 апреля 2026 | **Приоритет:** P2
**Статус:** 🐛 Открыт

## Описание
МеziyThrusterVisual настроен в Inspector (ParticleSystem + Directional Light),
но в Play Mode при активации мезиевых модулей визуал не отображается.

## Возможные причины
1. ParticleSystem не настроен (нет материала, emit = 0)
2. Light слишком слабый / не в той позиции
3. MeziyThrusterVisual.Activate() вызывается, но ParticleSystem не configured
4. Масштаб/позиция визуала не совпадает с соплами корабля

## Текущий код
`MeziyThrusterVisual.Activate()` вызывает `thrustParticle.Play()` и включает `glowLight`.
Но ParticleSystem должен быть предварительно настроен в Inspector.

## Предлагаемое решение
1. Создать префаб с настроенным ParticleSystem (material, emission, shape)
2. Добавить Debug.Log в Activate/Deactivate для проверки вызова
3. Проверить что ParticleSystem назначен и имеет материал
4. Настроить позицию визуала относительно корабля

## Затронутые файлы
- `Assets/_Project/Scripts/Ship/MeziyThrusterVisual.cs`
- (Новый) Префаб частиц сопла
