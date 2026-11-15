# T-MB01 — Источники и методология

> **Дата доступа:** 2026-11-15
> **Правило:** официальные источники приоритетнее; community-замеры помечены; непроверенное — помечено.

---

## 1. Официальные источники NVIDIA

| # | Источник | Что взято | Статус |
|---|---|---|---|
| 1 | [MotionBricks project page](https://nvlabs.github.io/motionbricks/) | Обзор, demo, Smart Locomotion/Object, code release | ✅ прочитано |
| 2 | [Paper (arXiv:2604.24833)](https://arxiv.org/abs/2604.24833) | Архитектура, root/pose/decoder, Smart Primitives, UE5 appendix, 15k FPS/2 ms, 350k clips | ✅ прочитано |
| 3 | [Paper PDF (SIGGRAPH 2026)](https://research.nvidia.com/labs/gear/motionbricks/pdfs/motionbricks_siggraph_2026.pdf) | DOI/TOG, детали | ✅ ссылка |
| 4 | [GitHub NVlabs/motionbricks](https://github.com/NVlabs/motionbricks) | Runtime API, ONNX, TensorRT, лицензии, ограничения, editors | ✅ прочитано |
| 5 | [Runtime API](https://github.com/NVlabs/motionbricks/blob/main/docs/runtime_api.md) | Context/target, generation flow, chaining, recorder, foot contacts, engines | ✅ прочитано |
| 6 | [ONNX inference](https://github.com/NVlabs/motionbricks/blob/main/docs/onnx_inference.md) | Tensor shapes, batch, latency table, TensorRT 10.8, RTX 4090 | ✅ прочитано |
| 7 | [SOMA model card](https://github.com/NVlabs/motionbricks/blob/main/docs/soma_model.md) | 30 joints, 200M params, 30 Hz, inputs/outputs, limitations, license | ✅ прочитано |
| 8 | [ONNX manifest `soma_n1.json`](https://github.com/NVlabs/motionbricks/blob/main/motionbricks/assets/onnx/soma/soma_n1.json) | Exact tensor names/shapes, joint order, opset 17 | ✅ прочитано |
| 9 | [HF nvidia/MotionBricks](https://huggingface.co/nvidia/MotionBricks) | Weights, model cards, license, no training pipeline | ✅ прочитано |
| 10 | [NVIDIA Open Model Agreement](https://www.nvidia.com/en-us/agreements/enterprise-software/nvidia-open-model-agreement/) | Commercial use, redistribution, outputs ownership, attribution | ✅ прочитано |
| 11 | [GR00T-WholeBodyControl / motionbricks preview](https://github.com/NVlabs/GR00T-WholeBodyControl/tree/main/motionbricks) | G1 preview, synthetic training pipeline | ✅ ссылка |
| 12 | [Kimodo project page](https://research.nvidia.com/labs/sil/projects/kimodo/) | 700 h, text + constraints, offline diffusion, SOMA/G1 | ✅ прочитано |
| 13 | [ARDY project page](https://research.nvidia.com/labs/sil/projects/ardy/) | Real-time autoregressive diffusion, SIGGRAPH 2026 | ✅ прочитано |
| 14 | [SOMA-X](https://github.com/NVlabs/SOMA-X) | Body model, skeleton | ✅ ссылка |
| 15 | [BONES-SEED dataset](https://huggingface.co/datasets/bones-studio/seed) | Training corpus context | ✅ ссылка |

---

## 2. Community-источники (неофициальные)

| # | Источник | Что взято | Оговорка |
|---|---|---|---|
| 16 | [ninja9998877/motionbricks-unity-bridge](https://github.com/ninja9998877/motionbricks-unity-bridge) | HTTP server + Unity C# client + Editor Clip Baker; SOMA→Humanoid mapping; limitations | Неофициально; Apache 2.0; проверить код |
| 17 | [bagybagy/Unity6_motionbricks_test](https://github.com/bagybagy/Unity6_motionbricks_test) | Unity 6000.3.13, G1 UDP, semantic retargeting; benchmark: ~818 MiB VRAM, 2.37 ms avg, spikes ~57.5 ms на RTX 5070 Ti | Неофициально; японский README; цифры — их замер |
| 18 | [IAFahim/sa-inbetween](https://github.com/IAFahim/sa-inbetween) | CPU ONNX Runtime, `.saib`, 77 joints, ~5 s / 50 frames, deterministic | Неофициально; Apache 2.0 code; модель отдельно |
| 19 | [localai-org/motion-bricks.cpp](https://github.com/localai-org/motion-bricks.cpp) | C++/GGUF port (существует) | **Не evaluated** |
| 20 | Reddit/LinkedIn посты о Unity-порте | Упоминания | **Не использованы**: 403/без первоисточника |

**Правило:** community-цифры (VRAM, latency) не считать официальными; они полезны как диапазон.

---

## 3. Unity

| # | Источник | Что взято |
|---|---|---|
| 21 | [Sentis overview 2.6](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/index.html) | Opset 7–25, платформы, backends |
| 22 | [Supported ONNX operators](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/supported-operators.html) | Список операторов/ограничений |
| 23 | [Supported models](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/supported-models.html) | 8-D limit, sparse/string/complex not supported |
| 24 | [Sentis models concept](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/models-concept.html) | Dynamic dimensions, optimization |
| 25 | [Package registry `com.unity.ai.inference`](https://packages.unity.com/com.unity.ai.inference) | 2.6.1, Unity 6000.0+, dependencies |
| 26 | [Playables API](https://docs.unity3d.com/6000.1/Documentation/Manual/Playables.html) | PlayableGraph/AnimationScriptPlayable |
| 27 | [Motion Matching for Unity (Asset Store)](https://assetstore.unity.com/packages/tools/animation/motion-matching-for-unity-145624) | Free, v2.3.2 (Mar 2025), original Unity 2021.3.45 |
| 28 | [JLPM22/MotionMatching](https://github.com/JLPM22/MotionMatching) | Open-source research package |
| 29 | [Animancer](https://kybernetik.com.au/animancer/) | Playable API wrapper, paid |
| 30 | [Unity Muse Animate docs](https://docs.unity3d.com/Packages/com.unity.muse.animate@1.0/manual/index.html) | Prompt-based editor tool, package 1.0 |
| 31 | [Unity Animation Rigging](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.4/manual/index.html) | TwoBoneIK/LookAt/ChainIK |

---

## 4. Project C

| # | Источник | Что взято |
|---|---|---|
| 32 | `AGENTS.md` | Tech stack, HARD RULES, FO, workflow |
| 33 | `ProjectSettings/ProjectVersion.txt` | Unity `6000.5.2f1` |
| 34 | `Packages/manifest.json` | URP 17.5, NGO 2.13, Cinemachine 3.1.7, Animation feature, **no Sentis** |
| 35 | `Packages/packages-lock.json` | `com.unity.animation.rigging 1.4.1`, Timeline 1.8.13 |
| 36 | `Assets/_Project/Prefabs/NetworkPlayer.prefab` | Humanoid Avatar, `applyRootMotion=0`, Animator controller |
| 37 | `Assets/_Project/Scripts/Player/NetworkPlayer.cs` | `CharacterController`, owner-authority `NetworkTransform` (`Interpolate=false`), movement/FO |
| 38 | `Assets/_Project/Scripts/AI/NpcBrain.cs` | Server FSM, `NavMeshAgent`, `NetworkTransform`, Animator params, skills |
| 39 | `Assets/_Project/Animations/PlayerAnimation.controller` | States: Idle, Walk_BlendTree, Sprint, Jump, Land, Attack1H, Skill, Gather, Lumber, TurnLeft/Right, Damage |
| 40 | `docs/Character/Skills/INP08_ANIMATOR_CLIP_PIPELINE.md` | `T-INP-08`: `AnimatorOverrideController`, `OnAttackImpact` ~60% |
| 41 | `docs/world/floatingorigin/09S_NEW_CONTENT_PIPELINE.md` | FO pipeline, NetworkObject registration, local anchors |
| 42 | `Assets/_Project/Scripts/World/FloatingOrigin/Network/GlobalMotionActorContract.cs` | `IGlobalMotionActorParticipant` |
| 43 | `Assets/_Project/Scripts/Equipment/Visual/EquipSlotToBone.cs` | `Animator.GetBoneTransform` for equipment |

**Инвентаризация ассетов (локально):**
- `*.fbx`: 1344 файла, ~6.31 GB (включая меши и анимации).
- `*.anim`: 13 файлов, ~20.5 MB.
- `*.controller`: 78 файлов.

---

## 5. Локальная методология аудита ONNX

### 5.1. Скачивание

```
https://media.githubusercontent.com/media/NVlabs/motionbricks/main/motionbricks/assets/onnx/soma/mmm_v6_soma_fp32.onnx
```

`Content-Length`: **757 864 273 B**.
FP16: **380 327 058 B**.

### 5.2. Парсинг

- Python 3.13, `onnx 1.23.2`.
- `onnx.ModelProto.ParseFromString`.
- Подсчёт `graph.node`, `graph.initializer`, `op_type`, атрибутов `Resize/ScatterElements/TopK/Range/OneHot/CumSum`.
- Результат: opset 17, 6993 nodes, 434 initializers, 51 unique op types.

### 5.3. CPU benchmark

- ONNX Runtime CPU (Python), `ORT_ENABLE_ALL`, `CPUExecutionProvider`.
- CPU: Intel Core i7-8700 (6 cores / 12 threads).
- Inputs: N=1/4/8, 8 slots, context 4 + target 1 (masks), `allowed_pred_num_tokens` all 1 (6..16).
- Poses: zeros + identity-like 6D rotations; **не** реальные позы.
- Warmup 1 run; 3 timed runs.
- Output: `[N,64,366]`, `pred_num_tokens=16`.
- Times: N=1 73.4/77.3/77.3 ms; N=4 146.3/156.2/156.9 ms; N=8 251.5/255.0/279.3 ms.
- **Не** измерялись: endpoint correction, boundary trim, decode, retarget, IK, качество.

### 5.4. Что не проверялось

- Официальный TensorRT runtime (нужен CUDA/TensorRT/Warp/Newton).
- Unity Sentis импорт/инференс.
- Unity Play Mode.
- Качество на реальных позах/персонажах.
- Сетевая синхронизация.
- Лицензионная экспертиза юристом.

---

## 6. Термины

| Термин | Значение |
|---|---|
| SOMA | Параметрическая модель человека NVIDIA; 30-суставный скелет |
| Smart Primitive | Высокоуровневый интерфейс: target-позы + interaction box + sequence |
| Context | 4 последние позы персонажа |
| Target | 1–4 позы, которых нужно достичь |
| Token | 4 кадра движения (30 Hz); 6–16 токенов на генерацию |
| VQ-VAE | Vector-Quantized VAE; декодер токенов в позы |
| Root module | Предсказывает длительность и траекторию root |
| Pose module | Masked transformer, предсказывает токены позы |
| Endpoint correction | Коррекция стыка сегментов (начало/конец) |
| Foot contacts | 4 confidence-значения (heel/toe L/R) |
| Retarget | Перенос позы с SOMA на Unity Humanoid |
| Baked clip | `.anim`, испечённый offline |

---

## 7. Итоговая оценка достоверности

| Утверждение | Достоверность |
|---|---|
| MotionBricks — реальный проект NVIDIA, SIGGRAPH 2026 | ✅ Официально |
| 200M params, 30 joints, 30 Hz, 18–58 frames | ✅ Model card/runtime docs |
| 758/380 MB ONNX | ✅ Локальный `Content-Length` |
| TensorRT 2.5/1.28 ms на RTX 4090 | ✅ Официальный benchmark |
| Community 2.37 ms/57.5 ms spike/818 MiB | ⚠️ Community, не официально |
| CPU 73–77 ms N=1 | ✅ Наш локальный замер, random input |
| Sentis op compatibility | ⚠️ Статический аудит, не импорт |
| Sentis latency | ❌ Не измерена |
| MMO-scale непригодность | ✅ Архитектурный вывод (не benchmark) |
| Лицензия commercial use | ✅ Agreement; юридическая экспертиза не проводилась |

---

*Все ссылки проверены 2026-11-15. При обновлении модели/доков перечитать [01_RESEARCH.md](01_RESEARCH.md) §2–3.*
