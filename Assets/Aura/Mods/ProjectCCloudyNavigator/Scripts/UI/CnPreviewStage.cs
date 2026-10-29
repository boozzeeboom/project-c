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
//   • объекты превью помечены HideFlags.DontSave и НЕ попадают в файл сцены. Сохранённая копия
//     оживает после загрузки без рантайм-материалов (магента) и камера превью рисует её поверх фигуры;
//   • корень превью уносится в DontDestroyOnLoad и перестаёт быть корнем мировой сцены. Иначе он —
//     участник ребейза (GlobalMotionControlledRebaseSlice:1113-1129 берёт КАЖДЫЙ активный корень по id
//     WORLD_SCENE_ROOT/<имя>) и вторая копия с тем же именем валит транзакцию целиком;
//   • камера превью видит ТОЛЬКО этот слой (`cullingMask = 1 << 30`), поэтому мир в кадр не попадает
//     и превью не зависит от того, где сейчас игрок и что делает floating origin;
//   • сетевых компонентов нет по построению: Transform / MeshFilter / MeshRenderer + Camera,
//     а коллайдеры, которые CreatePrimitive добавляет сам, уничтожаются сразу;
//   • камера выключена, пока окно скрыто, — скрытое окно не стоит ни одного рендер-прохода;
//   • свет сцены на превью не влияет: материал — свой студийный шейдер (Shaders/CN_Preview_studio),
//     а в сцене нет ни одного Light от порта. Иначе превью зависело бы от Sun/Moon мира
//     (в BootstrapScene Sun = 0.15, Moon = 1.0) и от ambientMode = Skybox с тёмным небом.
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

        /// <summary>Имя студийного шейдера превью: свет вшит в него, Light в сцене не нужен.</summary>
        private const string StudioShaderName = "Aura/CN/Preview Studio";

        /// <summary>Unlit-шейдер градиентной подложки превью.</summary>
        private const string BackdropShaderName = "Aura/CN/Preview Backdrop";

        private readonly VisualElement _element;
        private readonly GameObject _root;
        private readonly GameObject _rig;
        private readonly Camera _camera;
        private readonly RenderTexture _texture;
        private readonly Texture2D _backdropTex;   // вертикальный градиент студийного фона
        private Color _backdrop;                   // сплошная очистка камеры
        private readonly List<Material> _materials = new List<Material>(4);
        private readonly Dictionary<Material, Material> _studioCache = new Dictionary<Material, Material>(8);
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

            // Две преграды от сцены, обе обязательны — без них порт уже ломал игру (регресс T-FO06CY):
            //   • DontSave — объекты превью не должны сериализоваться в файл сцены. Сохранённая копия
            //     после загрузки остаётся без рантайм-материалов (магента) и камера превью рисует её
            //     поверх живой фигуры — в кадре появлялся чужой фон и лишняя модель;
            //   • DontDestroyOnLoad — объект уходит в свою сцену и перестаёт быть корнем мировой сцены,
            //     а ребейз перечисляет именно корни мировой сцены (GlobalMotionControlledRebaseSlice:1113-1129).
            //     Вторая копия с тем же именем валила транзакцию: participant_scope_refused:
            //     duplicate_participant_id:WORLD_SCENE_ROOT/[CN] Preview Stage.
            _root.hideFlags = HideFlags.DontSave;
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(_root);

            _rig = new GameObject("Rig");
            _rig.transform.SetParent(_root.transform, false);

            BuildFigure();

            var cameraGo = new GameObject("Preview Camera");
            cameraGo.transform.SetParent(_root.transform, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0.86f, -3f);

            // Подложка: вертикальный градиент (верх светлее) вместо цвета ниши станции. Грид цвета
            // убирает «дыру в мир» и даёт фигуре контражур, а не сливание с фоном.
            var top = ProjectCMainMenu.ProjectCMainMenuPalette.Hex("#313f39");
            var bottom = ProjectCMainMenu.ProjectCMainMenuPalette.Hex("#101814");
            _backdropTex = BuildBackdropTexture(top, bottom);
            _backdrop = new Color(bottom.r, bottom.g, bottom.b, 1f);

            // Фон — квад в сцене превью, а не цвет камеры и не Graphics.Blit: при Depth-очистке URP
            // дописывал в кадр небо мира (розовое небо и диск солнца), и фон переставал быть нашим.
            var backdropQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdropQuad.name = "Backdrop";
            backdropQuad.layer = PreviewLayer;
            backdropQuad.transform.SetParent(_root.transform, false);
            var backdropCollider = backdropQuad.GetComponent<Collider>();
            if (backdropCollider != null)
            {
                // Сразу выключаем, а Destroy отложен до конца кадра: иначе коллайдер успевает
                // отработать FixedUpdate (и попасть в сохранённую копию, если её кто-то сохранит).
                backdropCollider.enabled = false;
                UnityEngine.Object.Destroy(backdropCollider);
            }
            backdropQuad.transform.localPosition = new Vector3(0f, 0.86f, 2.5f);
            backdropQuad.transform.localScale = new Vector3(4f, 4f, 1f);

            var backdropRenderer = backdropQuad.GetComponent<MeshRenderer>();
            if (backdropRenderer != null)
            {
                backdropRenderer.sharedMaterial = CreateBackdropMaterial();
                backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;
                backdropRenderer.receiveShadows = false;
                backdropRenderer.lightProbeUsage = LightProbeUsage.Off;
                backdropRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

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

            // Своего света нет намеренно: он был направлен от камеры в спину фигуры
            // (Euler(28,152,0) → forward (0.41, −0.47, −0.78)) и вдобавок мог перехватить main light
            // у игровой камеры. Теперь весь свет считает шейдер CN_Preview_studio.

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
        /// Один кадр превью в RT. Весь буфер заливается фоновым цветом, потом камера рисует вид
        /// (градиентная подложка и фигура). Полная заливка нужна и для строк вне вьюпорта камеры:
        /// без неё там остаётся неинициализированная память текстуры (раньше — розовая полоса).
        /// </summary>
        private void RenderOnce()
        {
            if (_texture == null || _camera == null) return;

            var activeBefore = RenderTexture.active;
            RenderTexture.active = _texture;
            GL.Clear(true, true, _backdrop);
            RenderTexture.active = activeBefore;

            _camera.orthographicSize = OrthoSize / _zoom;
            _camera.Render();
        }

        /// <summary>Вертикальный градиент фона: две колонки пикселей, растягиваются блитом на весь RT.</summary>
        private static Texture2D BuildBackdropTexture(Color top, Color bottom)
        {
            const int stops = 64;
            var tex = new Texture2D(2, stops, TextureFormat.RGBA32, false)
            {
                name = "CN Preview Backdrop",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };

            for (int y = 0; y < stops; y++)
            {
                Color c = Color.Lerp(bottom, top, y / (stops - 1f));   // y = 0 — низ текстуры
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }

            tex.Apply(false, false);
            return tex;
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

            if (_backdropTex != null) UnityEngine.Object.Destroy(_backdropTex);
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
            ApplyStudioMaterials(_model);

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

            // CreatePrimitive сам вешает коллайдер: в превью физики нет, поэтому он выключается сразу и уничтожается —
            // Destroy отложен до конца кадра, а FixedUpdate может успеть раньше.
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
            }

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

        /// <summary>Материал подложки: unlit, только текстура градиента — свет сцены не участвует.</summary>
        private Material CreateBackdropMaterial()
        {
            Shader shader = Shader.Find(BackdropShaderName);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) return null;

            var material = new Material(shader) { name = "CN backdrop" };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", _backdropTex);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            _materials.Add(material);
            return material;
        }

        /// <summary>
        /// Студийный материал: свет вшит в шейдер, поэтому Light в сцене и ambient проекта
        /// на превью не влияют. Если шейдер не попал в сборку — падение на URP Unlit (тоже без света).
        /// </summary>
        private Material CreateStudioMaterial(string name, Color color, Texture texture)
        {
            Shader shader = Shader.Find(StudioShaderName);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) return null;

            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseMap") && texture != null) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_StudioGain")) material.SetFloat("_StudioGain", 1f);

            _materials.Add(material);
            return material;
        }

        private Material MakeMaterial(string name, Color color) => CreateStudioMaterial(name, color, null);

        /// <summary>
        /// Студийная копия игрового материала: берётся только base map и base color, остальное
        /// (металличность, карты нормалей, прозрачность) в 88 px не читается, а без своего света
        /// модель персонажа была почти чёрной. Исходные материалы игры не меняются — копия наша.
        /// </summary>
        private Material StudioFrom(Material source)
        {
            if (source == null) return MakeMaterial("CN studio", Color.white);

            if (_studioCache.TryGetValue(source, out Material cached) && cached != null) return cached;

            Texture texture = null;
            if (source.HasProperty("_BaseMap")) texture = source.GetTexture("_BaseMap");
            else if (source.HasProperty("_MainTex")) texture = source.GetTexture("_MainTex");

            Color color = Color.white;
            if (source.HasProperty("_BaseColor")) color = source.GetColor("_BaseColor");
            else if (source.HasProperty("_Color")) color = source.GetColor("_Color");

            Material material = CreateStudioMaterial(source.name + " (studio)", color, texture);
            _studioCache[source] = material;
            return material;
        }

        /// <summary>Все рендереры копии переводятся на студийный материал и перестают зависеть от света мира.</summary>
        private void ApplyStudioMaterials(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;

                Material[] source = renderer.sharedMaterials;
                if (source != null && source.Length > 0)
                {
                    var studio = new Material[source.Length];
                    for (int j = 0; j < source.Length; j++) studio[j] = StudioFrom(source[j]);
                    renderer.sharedMaterials = studio;
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }
    }
}
