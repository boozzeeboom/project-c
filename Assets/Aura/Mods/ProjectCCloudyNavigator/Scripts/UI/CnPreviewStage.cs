// Cloudy Navigator — превью фигуры персонажа в панели станции (шаг 6 плана).
//
// В моде здесь трёхмерный гуманоид из примитивов (.Aura/Slices/v4-2/src/character.js). В игре есть НАСТОЯЩАЯ
// модель игрока, и болванка из капсул — не то, что заказывал пользователь. Поэтому порядок такой:
//   1) когда в сцене есть локальный игрок — копируется его визуал (ребёнок «Visual_Model») и примитивы убираются;
//   2) пока игрока нет — показываются примитивы, то есть превью никогда не пустое.
//
// Копия — именно копия визуала, а не игрок: GameObject.Instantiate отвязывает объект от сети, а StripNonVisual
// снимает с копии Animator и все MonoBehaviour. Сетевых компонентов на «Visual_Model» нет по построению префаба,
// но проверка всё равно идёт по всей ветке: в окно не должен попасть ни один игровой компонент.
//
// Клик по фигуре (без драга) — запрос окна внешности: событие OnClick, решение принимает окно (CnActions).
//
// Изоляция от сцены игрока:
//   • объекты превью лежат на слое 30. Слой в TagManager не заведён, но по индексу работает —
//     настройки проекта не правятся, а поиск по Assets/_Project не находит ни `<< 30`, ни `.layer = 30`;
//   • камера превью видит ТОЛЬКО этот слой (`cullingMask = 1 << 30`), поэтому мир в кадр не попадает
//     и превью не зависит от того, где сейчас игрок и что делает floating origin;
//   • сетевых компонентов нет по построению: Transform / MeshFilter / MeshRenderer + Camera + Light,
//     а коллайдеры, которые CreatePrimitive добавляет сам, уничтожаются сразу;
//   • камера и свет выключены, пока окно скрыто, — скрытое окно не стоит ни одного рендер-прохода.
//
// Реакция на ввод — как в моде: драг вращает (pitch зажат ±25°), колесо меняет зум 0.8…1.6,
// через 4 с без ввода включается авто-вращение 12°/с.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace ProjectCCloudyNavigator.UI
{
    /// <summary>Превью фигуры: своя камера, свой RenderTexture, копия персонажа из примитивов.</summary>
    public sealed class CnPreviewStage : IDisposable
    {
        /// <summary>Независимый слой превью: в TagManager не заведён, в коде проекта не занят.</summary>
        public const int PreviewLayer = 30;

        private const int TextureSize = 256;
        private const float OrthoSize = 1.0f;      // 2 м по вертикали: фигура 1.75 м влезает с полями
        private const float MinZoom = 0.8f;        // мод: колесо 0.8…1.6
        private const float MaxZoom = 1.6f;
        private const float PitchLimit = 25f;      // мод: ±25°
        private const float AutorotateDelay = 4f;  // мод: 4 с без ввода
        private const float AutorotateSpeed = 12f; // градусов в секунду

        private readonly VisualElement _element;
        private readonly GameObject _root;
        private readonly GameObject _rig;
        private readonly Camera _camera;
        private readonly Light _light;
        private readonly RenderTexture _texture;
        private Color _backdrop;
        private readonly List<Material> _materials = new List<Material>(4);
        private readonly List<GameObject> _parts = new List<GameObject>(10);

        private GameObject _model;
        private float _modelTimer;

        private bool _visible;
        private bool _dragging;
        private int _pointerId = -1;
        private float _dragMoved;
        private float _yaw = 18f;
        private float _pitch;
        private float _yawVelocity;
        private float _zoom = 1f;
        private float _idle = AutorotateDelay;

        /// <summary>Клик по фигуре без драга: окно открывает окно внешности игры.</summary>
        public Action OnClick;

        public CnPreviewStage()
        {
            _root = new GameObject("[CN] Preview Stage");

            _rig = new GameObject("Rig");
            _rig.transform.SetParent(_root.transform, false);

            BuildFigure();

            var cameraGo = new GameObject("Preview Camera");
            cameraGo.transform.SetParent(_root.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0.86f, -3f);

            // Цвет подложки превью — тот же тёмный фон ниши станции. Им заливается весь RT перед каждым кадром.
            var backdrop = ProjectCMainMenu.ProjectCMainMenuPalette.Hex("#0b100d");
            _backdrop = new Color(backdrop.r, backdrop.g, backdrop.b, 1f);

            _texture = new RenderTexture(TextureSize, TextureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "CN Preview RT",
                antiAliasing = 1,   // MSAA не нужен: 256 px и так сглаживается билинейной выборкой
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            _texture.Create();

            _camera = cameraGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = OrthoSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _backdrop;
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 10f;
            _camera.allowHDR = false;
            _camera.targetTexture = _texture;
            _camera.enabled = false;   // окно закрыто до первого показа

            var lightGo = new GameObject("Preview Light");
            lightGo.transform.SetParent(_root.transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(28f, 152f, 0f);

            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.intensity = 1.05f;
            _light.color = new Color(0.93f, 0.96f, 0.93f);
            _light.shadows = LightShadows.None;
            _light.cullingMask = 1 << PreviewLayer;
            _light.enabled = false;

            _element = new VisualElement { name = "cn-preview" };
            _element.style.width = 88f;
            _element.style.height = 88f;
            _element.pickingMode = PickingMode.Position;
            _element.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_texture));

            _element.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _element.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _element.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _element.RegisterCallback<WheelEvent>(OnWheel);

            ApplyRotation();

            // Один кадр в RT сразу после сборки: без него текстура остаётся неинициализированной и UI Toolkit
            // показывает её как розовую заливку. Дальше кадры рисует Tick, пока окно видимо.
            RenderOnce();
        }

        /// <summary>Элемент превью: кладётся в рамку станции.</summary>
        public VisualElement Element => _element;

        /// <summary>Пока окно скрыто, камера и свет выключены — превью не рендерится вовсе.</summary>
        public bool Visible => _visible;

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible) _dragging = false;

            // Камера не включается никогда: окно рисует превью единым проходом в RenderOnce, иначе движок
            // рендерил бы её вторым проходом и результат зависел бы от порядка кадров.
            if (_camera != null) _camera.enabled = false;
            if (_light != null) _light.enabled = false;
        }

        /// <summary>Кадр: инерция, авто-вращение, зум. Пока окно скрыто — ничего не делает.</summary>
        public void Tick(float dt)
        {
            if (!_visible || _rig == null) return;

            float step = Mathf.Clamp(dt, 0f, 0.1f);

            if (!_dragging)
            {
                if (Mathf.Abs(_yawVelocity) > 0.01f)
                {
                    _yaw += _yawVelocity * step;
                    _yawVelocity = Mathf.Lerp(_yawVelocity, 0f, 1f - Mathf.Exp(-6f * step));
                }
                else
                {
                    _yawVelocity = 0f;
                }

                _idle += step;
                if (_idle > AutorotateDelay) _yaw += AutorotateSpeed * step;
            }

            if (_camera != null) _camera.orthographicSize = OrthoSize / _zoom;
            ApplyRotation();
            RenderOnce();

            // Модель игрока появляется не сразу: локальный игрок спавнится сетью уже после открытия окна.
            if (_model == null)
            {
                _modelTimer -= step;
                if (_modelTimer <= 0f)
                {
                    _modelTimer = 0.5f;
                    TryAttachPlayerModel();
                }
            }
        }

        /// <summary>
        /// Один кадр превью в RT. Сначала весь буфер заливается подложкой, потом камера рисует свой вид:
        /// камера в URP закрашивает только свой viewport, а остальные строки RT остаются такими, какими были, —
        /// именно так в Play Mode появлялась розовая верхняя половина превью (неинициализированная память
        /// текстуры). После полной очистки за пределами вида всегда подложка, а не мусор видеопамяти.
        /// </summary>
        private void RenderOnce()
        {
            if (_texture == null || _camera == null) return;

            var activeBefore = RenderTexture.active;
            RenderTexture.active = _texture;
            GL.Clear(true, true, _backdrop);
            RenderTexture.active = activeBefore;

            if (_light != null) _light.enabled = true;
            _camera.orthographicSize = OrthoSize / _zoom;
            _camera.Render();
            if (_light != null) _light.enabled = false;
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_texture != null)
            {
                if (_camera != null) _camera.targetTexture = null;
                _texture.Release();
                UnityEngine.Object.Destroy(_texture);
            }

            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] != null) UnityEngine.Object.Destroy(_materials[i]);
            }

            _materials.Clear();
        }

        // ------------------------------------------------------------------ ввод

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;

            _dragging = true;
            _pointerId = evt.pointerId;
            _dragMoved = 0f;
            _yawVelocity = 0f;
            _idle = 0f;
            _element.CapturePointer(evt.pointerId);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_dragging || evt.pointerId != _pointerId) return;

            Vector2 delta = evt.deltaPosition;
            _dragMoved += delta.magnitude;
            _yaw -= delta.x * 0.6f;
            _pitch = Mathf.Clamp(_pitch - delta.y * 0.4f, -PitchLimit, PitchLimit);
            _yawVelocity = -delta.x * 30f;
            _idle = 0f;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_dragging) return;

            _dragging = false;
            if (_element.HasPointerCapture(evt.pointerId)) _element.ReleasePointer(evt.pointerId);
            _pointerId = -1;
            _idle = 0f;

            // Драг вращает, клик — открывает внешность; порог тот же, что у руки на пиксельном экране (4 px).
            if (_dragMoved < 4f) OnClick?.Invoke();
        }

        private void OnWheel(WheelEvent evt)
        {
            // колесо вверх — ближе, вниз — дальше (как в моде)
            float factor = evt.delta.y > 0f ? 0.94f : 1.06f;
            _zoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
            evt.StopPropagation();
        }

        private void ApplyRotation()
        {
            if (_rig == null) return;
            _rig.transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // ------------------------------------------------------------------ настоящая модель игрока

        /// <summary>
        /// Копия визуала локального игрока вместо болванки. Берётся только ребёнок «Visual_Model»: там меши,
        /// скелет и материалы персонажа, но нет ни NetworkObject, ни NetworkPlayer. Копия ставится на слой превью,
        /// поэтому её видит только камера превью, и с живого игрока ничего не снимается.
        /// </summary>
        private void TryAttachPlayerModel()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            if (player == null) return;

            Transform visual = FindChild(player.transform, "Visual_Model");
            if (visual == null) return;

            _model = UnityEngine.Object.Instantiate(visual.gameObject, _rig.transform, false);
            _model.name = "Player Model";
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;
            _model.transform.localScale = Vector3.one;

            StripNonVisual(_model);
            SetLayerRecursive(_model, PreviewLayer);

            for (int i = 0; i < _parts.Count; i++)
            {
                if (_parts[i] != null) UnityEngine.Object.Destroy(_parts[i]);
            }

            _parts.Clear();
            RenderOnce();
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>С копии снимается всё, что не рисует: Animator, MonoBehaviour, коллайдеры, тела и источники света.</summary>
        private static void StripNonVisual(GameObject root)
        {
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null) UnityEngine.Object.Destroy(behaviours[i]);
            }

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null) UnityEngine.Object.Destroy(animators[i]);
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) UnityEngine.Object.Destroy(colliders[i]);
            }

            var bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] != null) UnityEngine.Object.Destroy(bodies[i]);
            }

            var lights = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null) UnityEngine.Object.Destroy(lights[i]);
            }
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            if (root == null) return;
            root.layer = layer;

            for (int i = 0; i < root.transform.childCount; i++)
            {
                SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
            }
        }

        // ------------------------------------------------------------------ фигура

        /// <summary>
        /// Фигура из примитивов — та же схема, что в моде (v4-1): торс, голова, руки, ноги, ступни.
        /// Размеры в метрах, рост ≈ 1.75; имена частей повторяют мод, чтобы правки вида читались однозначно.
        /// </summary>
        private void BuildFigure()
        {
            Color coat = ProjectCMainMenu.ProjectCMainMenuPalette.Hex("#2f7c50");
            Color trousers = ProjectCMainMenu.ProjectCMainMenuPalette.Hex("#1f2620");
            Color skin = ProjectCMainMenu.ProjectCMainMenuPalette.Hex("#d3e2e6");
            Color boots = ProjectCMainMenu.ProjectCMainMenuPalette.Hex("#121711");

            Material coatMat = MakeMaterial("CN coat", coat);
            Material trousersMat = MakeMaterial("CN trousers", trousers);
            Material skinMat = MakeMaterial("CN skin", skin);
            Material bootsMat = MakeMaterial("CN boots", boots);

            // Unity-капсула по умолчанию: высота 2, диаметр 1 → масштаб (диаметр, высота/2, диаметр).
            Part(PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.08f, 0f), new Vector3(0.36f, 0.275f, 0.36f), coatMat);
            Part(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.5f, 0f), new Vector3(0.26f, 0.26f, 0.26f), skinMat);
            Part(PrimitiveType.Capsule, "Arm L", new Vector3(-0.24f, 1.1f, 0f), new Vector3(0.12f, 0.25f, 0.12f), coatMat);
            Part(PrimitiveType.Capsule, "Arm R", new Vector3(0.24f, 1.1f, 0f), new Vector3(0.12f, 0.25f, 0.12f), coatMat);
            Part(PrimitiveType.Capsule, "Leg L", new Vector3(-0.1f, 0.4f, 0f), new Vector3(0.16f, 0.4f, 0.16f), trousersMat);
            Part(PrimitiveType.Capsule, "Leg R", new Vector3(0.1f, 0.4f, 0f), new Vector3(0.16f, 0.4f, 0.16f), trousersMat);
            Part(PrimitiveType.Cube, "Foot L", new Vector3(-0.1f, 0.04f, 0.03f), new Vector3(0.16f, 0.08f, 0.26f), bootsMat);
            Part(PrimitiveType.Cube, "Foot R", new Vector3(0.1f, 0.04f, 0.03f), new Vector3(0.16f, 0.08f, 0.26f), bootsMat);
        }

        private void Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = PreviewLayer;
            _parts.Add(go);

            // CreatePrimitive сам вешает коллайдер: в превью физики нет, поэтому он уничтожается сразу.
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);

            go.transform.SetParent(_rig.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        private Material MakeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.2f);

            _materials.Add(material);
            return material;
        }
    }
}
