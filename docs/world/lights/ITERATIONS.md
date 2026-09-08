# World Lighting — Итерации

## Итерация 1 от 2026-09-08

**Задача:** Провести аудит `lighting-plan.md`, утвердить поэтапную реализацию и зафиксировать baseline проекта перед изменениями Unity-ассетов.  
**Тикет:** `T-LIGHT01`  
**Коммит:** будет добавлен после фиксации этапа.

**Изменения:**
- `docs/world/lights/lighting-plan.md` — обновлён до версии 1.1; добавлены результаты аудита, корректировки плана, этапы и контрольные ворота.
- `docs/world/lights/implementation-log.md` — создан журнал реализации и baseline.
- `docs/world/lights/ITERATIONS.md` — создан локальный журнал этапов lighting.

**Подтверждено:**
- Unity `6000.5.2f1`, URP `17.5.0`.
- `ProjectC_URP.asset`: Additional Lights = `Per Vertex`, лимит `4`, Reflection Probe blending/box projection выключены.
- Найдены 24 `WorldScene_X_Y`.
- Найдены Day/Night/Twilight Volume Profiles.
- В просмотренных WorldScene отсутствуют назначенные LightingSettings.

**Ограничение:** Unity Editor не подключён к MCP for Unity bridge, поэтому Stage 1 и последующие изменения Unity-сцен/ассетов не запускались. В рабочее дерево не включались несвязанные изменения.

**Следующий этап:** `T-LIGHT02` — перевести Additional Lights на Per Pixel через Unity Editor и выполнить отдельную проверку.
