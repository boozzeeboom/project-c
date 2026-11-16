// Project C: Edit Mode preview 22 locomotion transition clips на Humanoid-персонаже.
// Не запускает Play Mode, не меняет Animator Controller, prefab или сцену: используется AnimationMode,
// который при закрытии окна возвращает исходную позу.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectC.Animation.MotionBricks.EditorTools
{
    public sealed class MotionBricksTransitionPreviewWindow : EditorWindow
    {
        private const string DefaultFolder =
            "Assets/_Project/Animations/Generated/MotionBricks/locomotion_transitions_v1/Male";

        [SerializeField] private GameObject _target;
        [SerializeField] private string _folder = DefaultFolder;
        [SerializeField] private int _selected;
        [SerializeField] private float _time;
        [SerializeField] private bool _playing;
        [SerializeField] private float _speed = 1f;

        private readonly List<AnimationClip> _clips = new List<AnimationClip>();
        private Vector2 _scroll;
        private bool _inAnimationMode;
        private bool _pausedByUser;

        [MenuItem("ProjectC/MotionBricks/Review/Locomotion Transition Preview")]
        private static void Open()
        {
            var window = GetWindow<MotionBricksTransitionPreviewWindow>("MB Transitions");
            window.minSize = new Vector2(440f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_target == null) _target = FindPreviewCharacter();
            RefreshClips();
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            StopPreview();
        }

        private void OnDestroy() => StopPreview();

        private void OnGUI()
        {
            EditorGUILayout.LabelField("MotionBricks — переходы на персонаже", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Edit Mode preview через AnimationMode. Play Mode не нужен; Controller, prefab и сцена не меняются.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                _target = (GameObject)EditorGUILayout.ObjectField("Preview character", _target, typeof(GameObject), true);
                if (GUILayout.Button("Use selected", GUILayout.Width(105f)))
                {
                    _target = Selection.activeGameObject;
                    StopPreview();
                }
                if (GUILayout.Button("Find", GUILayout.Width(60f)))
                {
                    _target = FindPreviewCharacter();
                    StopPreview();
                }
            }

            if (!HasHumanoid(_target))
            {
                EditorGUILayout.HelpBox(
                    "Нужен персонаж с Humanoid Avatar (например HumanM_Model в сцене MotionBricksTransitionPreview). " +
                    "Кликните по нему в Hierarchy и нажмите Use selected.",
                    MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _folder = EditorGUILayout.TextField("Folder", _folder);
                if (GUILayout.Button("Refresh", GUILayout.Width(80f))) RefreshClips();
            }

            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(245f));
            if (_clips.Count == 0)
            {
                EditorGUILayout.HelpBox("В папке нет Humanoid-клипов.", MessageType.Warning);
            }
            else
            {
                var labels = _clips.Select(c => c.name.Replace("MB_", "").Replace("_Humanoid", "")).ToArray();
                int next = GUILayout.SelectionGrid(_selected, labels, 2, EditorStyles.miniButton, GUILayout.Height(24f * ((labels.Length + 1) / 2)));
                if (next != _selected)
                {
                    _selected = next;
                    _time = 0f;
                    _playing = false;
                    _pausedByUser = false;
                }
            }
            EditorGUILayout.EndScrollView();

            if (_clips.Count == 0) return;

            EditorGUILayout.LabelField(
                $"{_selected + 1}/{_clips.Count}: {_clips[_selected].name}  ({_clips[_selected].length:F2} s)",
                EditorStyles.miniBoldLabel);
            float nextTime = EditorGUILayout.Slider("Time", _time, 0f, Mathf.Max(0.001f, _clips[_selected].length));
            if (!Mathf.Approximately(nextTime, _time)) _pausedByUser = false;
            _time = nextTime;
            _speed = EditorGUILayout.Slider("Speed", _speed, 0.1f, 2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ Prev")) SelectRelative(-1);
                if (GUILayout.Button(_playing ? "❚❚ Pause" : "▶ Play"))
                {
                    _playing = !_playing;
                    _pausedByUser = false;
                }
                if (GUILayout.Button("Next ▶")) SelectRelative(1);
                if (GUILayout.Button("Reset"))
                {
                    _time = 0f;
                    _pausedByUser = false;
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Stop / restore pose"))
                {
                    StopPreview();
                    _pausedByUser = true;
                }
                if (GUILayout.Button("Open preview scene")) OpenPreviewScene();
            }

            if (!HasHumanoid(_target) || _pausedByUser)
            {
                return;
            }

            if (_time > _clips[_selected].length) _time = _clips[_selected].length;
            Sample(_target, _clips[_selected], _time);

            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Space)
                {
                    _playing = !_playing;
                    e.Use();
                }
                else if (e.keyCode == KeyCode.LeftArrow)
                {
                    SelectRelative(-1);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.RightArrow)
                {
                    SelectRelative(1);
                    e.Use();
                }
            }
        }

        private void Tick()
        {
            if (!_playing || _clips.Count == 0 || !HasHumanoid(_target)) return;
            _time += Time.unscaledDeltaTime * _speed;
            if (_time > _clips[_selected].length) _time %= _clips[_selected].length;
            Sample(_target, _clips[_selected], _time);
            SceneView.RepaintAll();
            Repaint();
        }

        private void SelectRelative(int delta)
        {
            if (_clips.Count == 0) return;
            _selected = (_selected + delta + _clips.Count) % _clips.Count;
            _time = 0f;
            _pausedByUser = false;
            Sample(_target, _clips[_selected], _time);
            Repaint();
        }

        private void RefreshClips()
        {
            _clips.Clear();
            if (!AssetDatabase.IsValidFolder(_folder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { _folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("_Humanoid.anim", System.StringComparison.Ordinal)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null) _clips.Add(clip);
            }
            _clips.Sort((a, b) => System.StringComparer.OrdinalIgnoreCase.Compare(a.name, b.name));
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _clips.Count - 1));
        }

        private void Sample(GameObject target, AnimationClip clip, float time)
        {
            if (target == null || clip == null) return;
            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                _inAnimationMode = true;
            }
            AnimationMode.SampleAnimationClip(target, clip, Mathf.Clamp(time, 0f, clip.length));
            SceneView.RepaintAll();
        }

        private void StopPreview()
        {
            _playing = false;
            if (_inAnimationMode || AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
                _inAnimationMode = false;
            }
        }

        private static bool HasHumanoid(GameObject target)
        {
            if (target == null) return false;
            var animator = target.GetComponentInChildren<Animator>(true);
            return animator != null && animator.avatar != null && animator.avatar.isHuman;
        }

        private static GameObject FindPreviewCharacter()
        {
            var selected = Selection.activeGameObject;
            if (HasHumanoid(selected)) return selected;

            foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include))
                if (animator.avatar != null && animator.avatar.isHuman)
                    return animator.gameObject;

            return null;
        }

        private static void OpenPreviewScene()
        {
            const string scenePath = "Assets/_Project/Scenes/MotionBricksTransitionPreview.unity";
            if (File.Exists(scenePath))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(scenePath);
            }
            else
            {
                EditorUtility.DisplayDialog("Preview scene not found", scenePath, "OK");
            }
        }
    }
}
