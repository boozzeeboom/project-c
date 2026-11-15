# T-MB01A — Offline-пакет MotionBricks для агента интеграции

> **Дата:** 2026-11-15
> **Корень:** `C:\Projects\motion_bricks`
> **Корневой README пакета:** `C:\Projects\motion_bricks\README.md`
> **Зачем:** у агента, который будет делать интеграцию, **нет доступа к сети**. Все внешние ссылки из [README.md](README.md) / [01_RESEARCH.md](01_RESEARCH.md) / [02_PIPELINE.md](02_PIPELINE.md) продублированы локально здесь.
> **Статус:** пакет собран; **официальный TensorRT-прогон PASS** (RTX 3090, 26 + 50 кадров); CPU smoke-test PASS; Sentis-импорт ещё не проверялся (см. §10).

---

## 0. Quick start (offline)

```powershell
# 1. Официальный код и модели
cd C:\Projects\motion_bricks\motionbricks

# 2. CPU smoke-test (без NVIDIA)
C:\Projects\motion_bricks\tools\cpu_baker\.venv\Scripts\python.exe `
  C:\Projects\motion_bricks\tools\cpu_baker\smoke_test.py `
  --model C:\Projects\motion_bricks\motionbricks\motionbricks\assets\onnx\soma\mmm_v6_soma_fp32.onnx

# 3. Официальный TensorRT-путь (venv собран; проверен 2026-11-15)
.venv\Scripts\python.exe -c "import motionbricks, tensorrt; print(motionbricks.__version__, tensorrt.__version__)"
.venv\Scripts\python.exe docs\examples\generate_once.py --model soma

# 4. CPU CLI sa-inbetween (готовый пример)
cd C:\Projects\motion_bricks\community\sa-inbetween\releases\v0.9.4\tools-win-x64\SaInbetween
.\cli\sa-inbetween.exe inspect ..\..\library\demo_walk_to_jog.saib

# 5. Проверка целостности
Get-Content C:\Projects\motion_bricks\checksums\SHA256SUMS.txt
```

**Ожидаемо для CPU smoke-test:** `pred_global_motions (1, 64, 366)`, `pred_num_tokens [16]`, время ~50–80 ms на i7-8700. Это проверка «граф исполняется», не качества.

**Ключевые локальные ссылки (clickable):**
- [Корневой README пакета](file:///C:/Projects/motion_bricks/README.md)
- [Официальный README MotionBricks](file:///C:/Projects/motion_bricks/motionbricks/README.md)
- [Runtime API](file:///C:/Projects/motion_bricks/motionbricks/docs/runtime_api.md)
- [ONNX inference](file:///C:/Projects/motion_bricks/motionbricks/docs/onnx_inference.md)
- [SOMA model card](file:///C:/Projects/motion_bricks/motionbricks/docs/soma_model.md)
- [SOMA FP32 ONNX](file:///C:/Projects/motion_bricks/motionbricks/motionbricks/assets/onnx/soma/mmm_v6_soma_fp32.onnx)
- [HF SOMA model card](file:///C:/Projects/motion_bricks/models/huggingface/MotionBricks/soma/README.md)
- [Community Unity bridge](file:///C:/Projects/motion_bricks/community/motionbricks-unity-bridge/README.md)
- [sa-inbetween](file:///C:/Projects/motion_bricks/community/sa-inbetween/README.md)
- [Sentis tarball](file:///C:/Projects/motion_bricks/unity/packages/com.unity.ai.inference-2.6.1.tgz)
- [Paper SIGGRAPH 2026](file:///C:/Projects/motion_bricks/papers/motionbricks_siggraph_2026.pdf)
- [NVIDIA Open Model Agreement](file:///C:/Projects/motion_bricks/papers/nvidia-open-model-agreement-2026-04-02.pdf)
- [Checksums](file:///C:/Projects/motion_bricks/checksums/SHA256SUMS.txt)

---

## 1. Структура пакета

```
C:\Projects\motion_bricks\
├── README.md                     ← этот файл (инвентарь, offline-правила)
├── motionbricks\                 ← официальный репозиторий NVlabs/motionbricks
│   ├── README.md
│   ├── docs\                     ← runtime_api, onnx_inference, soma_model, tutorial
│   ├── motionbricks\assets\onnx\ ← SOMA/G1 ONNX (FP32/FP16)
│   ├── motionbricks\assets\...    ← SOMA USD, G1 meshes, sample motions
│   ├── tools\onnx\               ← ONNX exporter
│   ├── app\                      ← демо/редакторы Smart Primitives
│   ├── .venv\                    ← официальное окружение (TensorRT), собранное
│   └── .trt-cache\               ← TensorRT plan cache (~0.7 GB)
├── models\huggingface\MotionBricks\ ← SafeTensors SOMA/G1 + model cards
├── community\
│   ├── motionbricks-unity-bridge\   ← HTTP server + Unity C# + Editor Clip Baker
│   ├── sa-inbetween\                ← CPU ONNX CLI/Unity package + releases\v0.9.4 (library/demo)
│   ├── Unity6_motionbricks_test\    ← Unity 6 G1/UDP/retarget reference project
│   └── motion-bricks.cpp\           ← C++/GGUF port (optional)
├── unity\packages\               ← Sentis tarball + dependencies (offline UPM)
├── papers\                       ← SIGGRAPH/arXiv PDF, NVIDIA Open Model Agreement
├── tools\cpu_baker\              ← CPU venv + smoke/inspect scripts
├── tools\python312\              ← bundled CPython 3.12 (для official .venv)
├── tools\uv\uv.exe               ← bundled uv 0.13.7
└── checksums\SHA256SUMS.txt      ← SHA256 ключевых артефактов
```

**Общий размер пакета:** ~9.4 GB (из них ~1.5 GB — HF SafeTensors, ~1.1 GB — community (включая копию SOMA ONNX для `sa-inbetween`), ~0.4 GB — Unity tarballs, ~0.7 GB — TensorRT plan cache, ~3 GB — official `.venv`).

---

## 2. Официальные ресурсы (локально)

| Что | Локальный путь | Origin | Размер |
|---|---|---|---|
| Официальный repo | `C:\Projects\motion_bricks\motionbricks` | `github.com/NVlabs/motionbricks` | ~6.0 GB (code+ONNX+LFS ~2.3 GB, `.venv` ~3 GB, `.trt-cache` ~0.7 GB) |
| README / обзор | `motionbricks\README.md` | GitHub | — |
| Runtime API | `motionbricks\docs\runtime_api.md` | GitHub | — |
| ONNX inference | `motionbricks\docs\onnx_inference.md` | GitHub | — |
| SOMA model card | `motionbricks\docs\soma_model.md` | GitHub | — |
| G1 model card | `motionbricks\docs\unitree_g1_model.md` | GitHub | — |
| Guided tutorial | `motionbricks\docs\guided_tutorial.md` | GitHub | — |
| SOMA FP32 ONNX | `motionbricks\motionbricks\assets\onnx\soma\mmm_v6_soma_fp32.onnx` | GitHub LFS | 758 MB |
| SOMA FP16 ONNX | `motionbricks\motionbricks\assets\onnx\soma\mmm_v6_soma_fp16.onnx` | GitHub LFS | 380 MB |
| G1 FP32 ONNX | `motionbricks\motionbricks\assets\onnx\unitree_g1\mmm_v6_g1_fp32.onnx` | GitHub LFS | 734 MB |
| G1 FP16 ONNX | `motionbricks\motionbricks\assets\onnx\unitree_g1\mmm_v6_g1_fp16.onnx` | GitHub LFS | 369 MB |
| SOMA tensor manifest | `motionbricks\motionbricks\assets\onnx\soma\soma_n1.json` | GitHub | 2 KB |
| G1 tensor manifest | `motionbricks\motionbricks\assets\onnx\unitree_g1\unitree_g1_n1.json` | GitHub | 2 KB |
| SOMA skeleton USD | `motionbricks\motionbricks\assets\human\soma_base_skel_minimal_no_root.usd` | GitHub LFS | 2 MB |
| Sample motions (SOMA BVH) | `motionbricks\assets\motions\soma\` | GitHub LFS | ~100 MB |
| Sample motions (G1 CSV) | `motionbricks\assets\motions\unitree_g1\` | GitHub LFS | ~10 MB |
| ONNX exporter | `motionbricks\tools\onnx\` | GitHub | — |
| Демо/редакторы | `motionbricks\app\` | GitHub | — |
| Лицензии зависимостей | `motionbricks\licenses\` | GitHub | — |
| Paper PDF (SIGGRAPH) | `papers\motionbricks_siggraph_2026.pdf` | NVIDIA Research | 48 MB |
| Paper PDF (arXiv) | `papers\motionbricks_arxiv_2604.24833.pdf` | arXiv | 49 MB |
| NVIDIA Open Model Agreement | `papers\nvidia-open-model-agreement-2026-04-02.pdf` | NVIDIA | 67 KB |

**Важно:** sample motions (BONES BVH/CSV) — **eval-only** по NVIDIA Sample Data Evaluation License. Не шипить и не использовать как production-контент. См. `motionbricks\assets\motions\LICENSE.txt` и `README.md`.

---

## 3. Веса Hugging Face (SafeTensors)

| Что | Локальный путь |
|---|---|
| SOMA pose (520.8 MB) | `models\huggingface\MotionBricks\soma\soma-v6-nova-4n8g-ea43dd8-fp32-v1\mmm-v6-soma-pose-nova-4n8g-ea43dd8-fp32-v1\version_0\checkpoints\model-step=1500000.safetensors` |
| SOMA root (154.2 MB) | `...\mmm-v6-soma-root-nova-4n8g-ea43dd8-fp32-v1\version_0\checkpoints\model-step=1500000.safetensors` |
| SOMA VQ-VAE (89.1 MB) | `...\vqvae-v6-soma-nova-4n8g-ea43dd8-fp32-v1\version_0\checkpoints\model-step=1500000.safetensors` |
| G1 pose (521.1 MB) | `...\g1\out\motionbricks_pose\version_1\checkpoints\model-step=2000000.safetensors` |
| G1 root (130.2 MB) | `...\g1\out\motionbricks_root\version_1\checkpoints\model-step=2000000.safetensors` |
| G1 VQ-VAE (90.8 MB) | `...\g1\out\motionbricks_vqvae\version_1\checkpoints\model-step=2000000.safetensors` |

Model cards: `models\huggingface\MotionBricks\soma\README.md`, `...\g1\README.md`.
Лицензия весов: NVIDIA Open Model Agreement (коммерческое использование/распространение при соблюдении terms).

---

## 4. Community-референсы

| Репозиторий | Локальный путь | Зачем | Оговорка |
|---|---|---|---|
| `ninja9998877/motionbricks-unity-bridge` | `community\motionbricks-unity-bridge` | HTTP server + Unity C# + Editor Clip Baker; SOMA→Humanoid mapping | Неофициально; Apache 2.0; проверить код |
| `IAFahim/sa-inbetween` | `community\sa-inbetween` | CPU ONNX Runtime CLI/Unity package; `.saib`; reference для offline bake | Неофициально; модель отдельно |
| `sa-inbetween` releases v0.9.4 | `community\sa-inbetween\releases\v0.9.4\` | Готовый `win-x64` CLI + tools (`bake`, `cli`, `model`, `humanoid-v1.safamily`); модель и `soma_rest.pose` уже положены в `tools-win-x64\SaInbetween`; `library\` — тестовые позы | Windows x64; .NET 10 self-contained; `library` — **eval-only** |
| `bagybagy/Unity6_motionbricks_test` | `community\Unity6_motionbricks_test` | Unity 6000.3.13, G1/UDP, semantic retargeting, VRAM/latency benchmark | Неофициально; японский README; LFS/submodule не тянулись |
| `localai-org/motion-bricks.cpp` | `community\motion-bricks.cpp` | C++/GGUF port (alternative runtime) | Не evaluated |

**Не копировать код в `Assets/` без review и attribution.**

---

## 5. Unity-пакеты (offline UPM)

| Пакет | Локальный путь | Версия |
|---|---|---|
| Sentis / Inference Engine | `unity\packages\com.unity.ai.inference-2.6.1.tgz` | 2.6.1 |
| Burst | `unity\packages\com.unity.burst-1.8.17.tgz` | 1.8.17 |
| Collections | `unity\packages\com.unity.collections-2.4.3.tgz` | 2.4.3 |
| Newtonsoft JSON | `unity\packages\com.unity.nuget.newtonsoft-json-3.2.1.tgz` | 3.2.1 |
| App UI | `unity\packages\com.unity.dt.app-ui-1.3.6.tgz` | 1.3.6 |
| Mathematics | `unity\packages\com.unity.mathematics-1.2.1.tgz` | 1.2.1 |
| Mono Cecil | `unity\packages\com.unity.nuget.mono-cecil-1.11.4.tgz` | 1.11.4 |
| Test Framework Performance | `unity\packages\com.unity.test-framework.performance-3.0.3.tgz` | 3.0.3 |

**Установка (в отдельной ветке!):**
1. Unity → `Window > Package Manager`.
2. `+` → `Install package from tarball...` → выбрать `com.unity.ai.inference-2.6.1.tgz`.
   Если в вашей версии Unity нет пункта tarball — распакуйте `.tgz` (`tar -xzf ...`) и используйте `Add package from disk...` → `package/package.json`.
3. Если Unity попросит зависимости — установить tarball'ы из этой папки (тем же способом).
4. Проверить Console; затем открыть `Assets` → model import.

**Конфликт версий (важно):**
- Sentis 2.6.1 декларирует Burst **1.8.17**, Collections **2.4.3**, Newtonsoft **3.2.1**.
- В Project C уже стоят Burst **1.8.29**, Collections **6.5.0**, Newtonsoft **3.2.2**, Mathematics **1.4.0**.
- UPM может попытаться downgrade'ить зависимости и сломать другие пакеты. **Не менять `manifest.json` в main без отдельного решения.** Сначала проверить в ветке/копии проекта; при конфликте — искать более новый Sentis или ставить его в изолированный тестовый проект.

---

## 6. Python-окружения

### 6.1. CPU-only (рекомендуется для offline baker)

| Параметр | Значение |
|---|---|
| Venv | `C:\Projects\motion_bricks\tools\cpu_baker\.venv` |
| Python | 3.13 |
| Пакеты | `onnx 1.23.2`, `onnxruntime 1.32.0`, `numpy 2.5.4`, `fastapi`, `uvicorn` |
| Lock | `tools\cpu_baker\requirements.lock.txt` |
| Smoke test | `tools\cpu_baker\smoke_test.py` |
| Inspect graph | `tools\cpu_baker\inspect_onnx.py` |

Плюсы: не нужен NVIDIA/CUDA/TensorRT; работает на CPU; детерминированно.
Минусы: нет endpoint correction/boundary trim официального engine; качество может отличаться; 758 MB модель.

### 6.2. Официальный TensorRT (если `.venv` собран)

| Параметр | Значение |
|---|---|
| Venv | `C:\Projects\motion_bricks\motionbricks\.venv` |
| Python | 3.12.12, bundled base: `C:\Projects\motion_bricks\tools\python312` |
| Пакеты | `newton`, `warp-lang`, `tensorrt-cu12 10.8.0.43`, `usd-core`, `imgui-bundle`, ... |
| Запуск | `cd C:\Projects\motion_bricks\motionbricks; .venv\Scripts\python.exe ...` (или `uv run --offline python ...`) |
| Проверка | **PASS** 2026-11-15: `uv run python docs/examples/generate_once.py --model soma` → 26 frames (0.87 s) + 50 frames (1.67 s), RTX 3090 24 GB, driver 610.88, CUDA 12.9 |

Плюсы: официальный engine, endpoint correction, максимальное качество.
Минусы: NVIDIA GPU + CUDA-совместимый драйвер; первый запуск строит TensorRT plan (минуты); `.venv` тяжёлый.

### 6.3. `uv`

`uv 0.13.7` лежит в `C:\Projects\motion_bricks\tools\uv\uv.exe` (копия глобального). Для offline:
```powershell
cd C:\Projects\motion_bricks\motionbricks
C:\Projects\motion_bricks\tools\uv\uv.exe run --offline python -c "import motionbricks; print(motionbricks.__version__)"
```
Проверено: `uv run --offline` работает; `uv sync` без сети не запускать (может попытаться обновить lock/venv).

---

## 7. Проверка целостности

```powershell
Get-Content C:\Projects\motion_bricks\checksums\SHA256SUMS.txt
# точечно:
(Get-FileHash C:\Projects\motion_bricks\motionbricks\motionbricks\assets\onnx\soma\mmm_v6_soma_fp32.onnx -Algorithm SHA256).Hash
```

Ожидаемые SHA256 для ONNX совпадают с LFS OID (см. `git lfs ls-files --size` в official repo).

---

## 8. Лицензии и attribution

| Компонент | Лицензия | Что делать |
|---|---|---|
| Код MotionBricks | Apache 2.0 | Сохранить `LICENSE`, notices |
| Модели SOMA/G1 (ONNX, SafeTensors) | NVIDIA Open Model Agreement | При распространении — приложить agreement/notice; `Licensed by NVIDIA Corporation under the NVIDIA Open Model Agreement.` |
| Sample motions (BONES BVH/CSV) | NVIDIA Sample Data Evaluation License | **Только eval/test; не шипить** |
| Community bridges | Apache 2.0 (заявлено) | Проверить, сохранить attribution |
| Unity Sentis/Burst/Collections | Unity Companion License / package terms | Не перепродавать; использовать в Unity-проекте |

---

## 9. Что НЕ входит в пакет

- NVIDIA driver / CUDA toolkit — уже стоят на машине (RTX 3090, driver 610.88); для другой машины ставить отдельно.
- BONES RP1 training dataset — не распространяется; sample motions только eval.
- `community\sa-inbetween\releases\v0.9.4\library` — позы, сгенерированные из bundled sample motions; **eval-only**, не шипить.
- Полный SOMA-X body model / meshes — не нужен для Unity-ретаргета; в repo есть skeleton USD.
- `GR00T-WholeBodyControl` preview / training pipeline — не скачивался; для инференса не нужен (только если понадобится обучение/дообучение).
- Kimodo/ARDY модели — альтернативы, не для T-MB02.
- Unity-проект Project C — не изменялся; Sentis не установлен.

---

## 10. Статус проверок

| Проверка | Статус | Детали |
|---|---|---|
| CPU ONNX Runtime smoke-test | ✅ **PASS** | `smoke_test.py`, SOMA FP32, 55–71 ms N=1, output `[1,64,366]`, tokens 16 |
| Официальный TensorRT / `generate_once.py` | ✅ **PASS** | RTX 3090 24 GB, driver 610.88, CUDA 12.9; 26 frames (0.87 s) + 50 frames (1.67 s); TensorRT plan закэширован |
| Полный `scene_preview` (viewer) | ⚠️ Не запускался | Нужен GL/viewer; не обязателен для baker |
| Sentis import | ❌ Не проверялся | Статический op-аудит хороший, но 6993 узла/динамические shape не проверены |
| Unity Play Mode | ❌ Не запускался | Правило `dont-start-play` |
| Community bridge | ⚠️ Код не ревьюился построчно | Использовать как reference |
| `sa-inbetween` CLI | ✅ **PASS** | `tools-win-x64\SaInbetween\cli\sa-inbetween.exe generate` → `library\demo_walk_to_jog.saib`, 50 frames; `inspect` OK |
| Checksums | ✅ Ключевые артефакты | Sample motions/меши не хэшировались |

**TensorRT plan cache:** первый запуск `generate_once.py` строит plan (несколько минут); кэш — в `C:\Projects\motion_bricks\motionbricks\.trt-cache\` (проверить путь; официальный runtime использует `.trt-cache`). Повторный запуск быстрее.

---

## 11. Mapping: external → local

| Внешний URL | Локально |
|---|---|
| `https://nvlabs.github.io/motionbricks/` | `motionbricks\README.md`, `papers\motionbricks_siggraph_2026.pdf` |
| `https://github.com/NVlabs/motionbricks` | `motionbricks\` |
| `https://github.com/NVlabs/motionbricks/blob/main/docs/runtime_api.md` | `motionbricks\docs\runtime_api.md` |
| `https://github.com/NVlabs/motionbricks/blob/main/docs/onnx_inference.md` | `motionbricks\docs\onnx_inference.md` |
| `https://github.com/NVlabs/motionbricks/blob/main/docs/soma_model.md` | `motionbricks\docs\soma_model.md` |
| `https://github.com/NVlabs/motionbricks/blob/main/docs/guided_tutorial.md` | `motionbricks\docs\guided_tutorial.md` |
| `https://github.com/NVlabs/motionbricks/tree/main/motionbricks/assets/onnx/soma` | `motionbricks\motionbricks\assets\onnx\soma\` |
| `https://huggingface.co/nvidia/MotionBricks` | `models\huggingface\MotionBricks\` |
| `https://github.com/ninja9998877/motionbricks-unity-bridge` | `community\motionbricks-unity-bridge\` |
| `https://github.com/IAFahim/sa-inbetween` | `community\sa-inbetween\` |
| `https://github.com/bagybagy/Unity6_motionbricks_test` | `community\Unity6_motionbricks_test\` |
| `https://github.com/localai-org/motion-bricks.cpp` | `community\motion-bricks.cpp\` |
| `https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/index.html` | `unity\packages\com.unity.ai.inference-2.6.1.tgz` (docs внутри tarball) |
| `https://www.nvidia.com/en-us/agreements/enterprise-software/nvidia-open-model-agreement/` | `papers\nvidia-open-model-agreement-2026-04-02.pdf` |

---

*Пакет собран 2026-11-15. При обновлении official repo — повторить `git pull`, `git lfs pull`, `hf download`, пересчитать checksums.*
