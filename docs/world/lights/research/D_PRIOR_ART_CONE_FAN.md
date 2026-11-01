# Подотчёт D — Prior art: конус/сетка/бюджет/гистерезис/fade/LOD/подстановка/pooling

**Сабагент:** general (web) · **Дата:** 2026-11-01 · **Задача:** web-ресерч решений для активации/деактивации многих ламп в больших мирах.

## 1. Конусная/веерная активация (cone/fan)

- Фронтальный конус + малый тыловой радиус. Ссылки: clustered shading, sphere-sliced cone culling (Bart Wronski, Eric's Blog, GDC 2016).
- Подход: активировать лампу, если она в радиусе И в конусе перед камерой/игроком; позади — только в малом радиусе.
- Углы обычно 60–90° по горизонтали, 30–45° по вертикали; тыловой радиус 15–30 м.

## 2. Секторная/сеточная активация (spatial grid)

- Двухуровневая сетка (cell → region), окрестность 5×5, примеры AAEmu, OpenRA, The Division, HLOD.
- Делит мир на ячейки (например 32×32 м); активирует ячейки в радиусе и в направлении взгляда; все лампы в активных ячейках — кандидаты.
- Дешевле O(N) при большом N, но сложнее в реализации и отладке. Полезно при > 5 000 ламп.

## 3. Бюджетная nearest-N с direction weighting

- Score = dist + angle penalty; partial sort; ReSTIR/RIS; Unity URP budget (32 authored lights); Drift engine.
- Кандидаты сортируются по score; фронтальные (малый угол) получают приоритет при cap.
- Partial sort (nth_element) вместо полной сортировки экономит CPU.

## 4. Гистерезис и пре-активация (pre-activation)

- Двойной порог (R_on/R_off), cooldown кадры, HISM cull distances, De Leon Dev LOD.
- onDist ≠ offDist (offDist = onDist × 1.1–1.2) против мигания на границе.
- Pre-activation margin (+10–15° / +10 м) для упреждения при повороте камеры.

## 5. Плавное затухание (fade)

- Lerp интенсивности, fade band (Drift/flutter3d), Unity официальная рекомендация, Unreal Timeline.
- Замена мгновенного toggle на fade-in (0.15 с) / fade-out (0.30 с) убирает «щелчки».
- `enabled` остаётся true до конца fade-out, затем false.

## 6. Light LOD

- Point → emissive → probe; UnityLightsLodSystem, Unity emissive/LOD0 issue, Enlighten, Killzone, Doom Eternal.
- Близко: точечный источник; средне: только emissive корпус; далеко: ничего (или L2-квад).
- Снижает число активных источников в 2–3 раза.

## 7. Подстановка (substitution)

- H_FakeLights (decal), DriftLight (summed volume), Killzone probes, The Division PRT, Frostbite lightmaps, Far Cry 3 DRTV.
- Замена части точечных источников на light probes / emissive-only. Уменьшает нагрузку, но теряет динамическое освещение интерьера.
- Подходит для статичного окружения; realtime point lights оставить для динамических акцентов.

## 8. Pooling best practices / pitfalls

10 частых ошибок:
1. Focus lag (слишком редкий пересчёт фокуса).
2. Camera vs player (выбор не той камеры).
3. Sorting cost (полная сортировка вместо partial).
4. Shader recompilation flicker (вкл/выкл Light вызывает перекомпиляцию шейдеров в некоторых пайплайнах).
5. Shadow cost (point light shadows = 6 граней куба).
6. Per-light MonoBehaviour overhead.
7. Отсутствие гистерезиса → мигание на границе.
8. Одинаковые яркие света → непредсказуемый top-N.
9. Нет пространственной индексации → O(N) растёт линейно.
10. Хранение мировых координат → ломается при Floating Origin.

## Рекомендованная 12-шаговая архитектура (для Unity-случая)

1. Один LightManager (вместо множества компонентов).
2. Spatial grid (32×32 м).
3. Конусная фильтрация (фронтальный конус + тыловой радиус).
4. Бюджет N (cap 96–128).
5. Гистерезис (onDist/offDist).
6. Fade (0.15/0.30 с).
7. LOD (point → emissive → probe).
8. Pooling (переиспользование Light-компонентов).
9. Camera-as-key (фокус = камера/игрок).
10. Pre-activation margin.
11. Day/night emissive.
12. Интеграция с HLOD L2.

Сводный список из ~50 источников с URL (см. оригинальный отчёт сабагента D).

*Источник: подотчёт сабагента D.*