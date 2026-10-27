// ProjectC Main Menu — the sagging cables and the little ferry that crosses under
// them (ported from src/ropes.js). Ribbons are rebuilt every frame (3 ropes x 57
// stations) so the sag and the sway stay smooth; the ferry samples the same curve
// it hangs from.
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectCMainMenu
{
    public class ProjectCMainMenuRopeRig : MonoBehaviour
    {
        private const int Segments = 56;

        [SerializeField] private Material ropeMaterial;
        [SerializeField] private Material hullMaterial;
        [SerializeField] private Material glowMaterial;

        private sealed class Rope
        {
            public GameObject Go;
            public Mesh Mesh;
            public Renderer Renderer;
            public MaterialPropertyBlock Block;
            public Vector3[] Verts;
            public Vector2[] Samples;
            public float Yc;
            public float Sag;
            public float Width;
            public float Opacity;
            public float Sway;
            public float Phase;
        }

        private readonly Rope[] _ropes = new Rope[3];
        private Transform _cart;
        private Renderer _cartHull;
        private Renderer _cartGlow;
        private Renderer _cartCabin;
        private Renderer _cartRim;
        private Renderer _cartMast;
        private Renderer _cartPulley;
        private MaterialPropertyBlock _cartBlock;

        private float _width = 1f;
        private float _height = 1f;
        private float _time;
        private float _phase = 1.1f;
        private float _tilt;
        private float _face = 1f;
        private float _scale = 0.05f;
        private float _depth = 0.84f;
        private bool _cartStarted;
        private float _lastStep;
        private bool _built;

        public float CartX { get; private set; } = 0.5f;
        public float CartDir { get; private set; } = 1f;
        public float RopeMidY { get; private set; }
        public float CartSize { get; private set; } = 0.038f;
        public float CartSway { get; private set; }

        public void Initialize()
        {
            if (_built) return;
            _built = true;

            var presets = new[]
            {
                new { Yc = 0.330f, Sag = 0.052f, Width = 0.0014f, Opacity = 0.26f, Sway = 0.8f, Phase = 0.6f, Z = 0.9965f },
                new { Yc = 0.215f, Sag = 0.072f, Width = 0.0021f, Opacity = 0.42f, Sway = 1.0f, Phase = 2.3f, Z = 0.9961f },
                new { Yc = 0.100f, Sag = 0.048f, Width = 0.0016f, Opacity = 0.30f, Sway = 1.2f, Phase = 4.5f, Z = 0.9957f },
            };

            for (var i = 0; i < _ropes.Length; i++)
            {
                var preset = presets[i];
                var go = new GameObject("Rope_" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, preset.Z);

                var mesh = new Mesh { name = "PCMM_Rope_" + i };
                mesh.MarkDynamic();
                var verts = new Vector3[(Segments + 1) * 2];
                var uv = new Vector2[(Segments + 1) * 2];
                var tris = new int[Segments * 6];
                for (var s = 0; s <= Segments; s++)
                {
                    var t = (float)s / Segments;
                    var fade = Mathf.Min(1f, t / 0.07f) * Mathf.Min(1f, (1f - t) / 0.07f);
                    uv[s * 2] = new Vector2(0f, fade);
                    uv[s * 2 + 1] = new Vector2(1f, fade);
                    if (s < Segments)
                    {
                        var a = s * 2;
                        var ti = s * 6;
                        tris[ti] = a; tris[ti + 1] = a + 1; tris[ti + 2] = a + 2;
                        tris[ti + 3] = a + 1; tris[ti + 4] = a + 3; tris[ti + 5] = a + 2;
                    }
                }
                mesh.vertices = verts;
                mesh.uv = uv;
                mesh.triangles = tris;
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200f);

                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ropeMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.sortingOrder = 34 + i;

                _ropes[i] = new Rope
                {
                    Go = go,
                    Mesh = mesh,
                    Renderer = renderer,
                    Block = new MaterialPropertyBlock(),
                    Verts = verts,
                    Samples = new Vector2[Segments + 1],
                    Yc = preset.Yc,
                    Sag = preset.Sag,
                    Width = preset.Width,
                    Opacity = preset.Opacity,
                    Sway = preset.Sway,
                    Phase = preset.Phase,
                };
            }

            BuildCart();
        }

        private void BuildCart()
        {
            var cartGo = new GameObject("FerryCart");
            cartGo.transform.SetParent(transform, false);
            cartGo.transform.localPosition = new Vector3(0f, 0f, 0.9955f);
            _cart = cartGo.transform;
            _cartBlock = new MaterialPropertyBlock();

            _cartHull = AddQuad(_cart, "Hull", hullMaterial, new Vector3(2.0f, 0.86f, 1f), new Vector3(0f, 0f, 0f), 35);
            _cartCabin = AddQuad(_cart, "Cabin", ropeMaterial, new Vector3(1.15f, 0.56f, 1f), new Vector3(0.04f, 0.58f, 0.002f), 35);
            _cartRim = AddQuad(_cart, "Rim", ropeMaterial, new Vector3(1.98f, 0.06f, 1f), new Vector3(0f, 0.30f, 0.003f), 35);
            _cartMast = AddQuad(_cart, "Mast", ropeMaterial, new Vector3(0.045f, 0.52f, 1f), new Vector3(-0.42f, 1.02f, 0.003f), 35);
            _cartPulley = AddQuad(_cart, "Pulley", ropeMaterial, new Vector3(0.10f, 0.10f, 1f), new Vector3(0f, 1.36f, 0.006f), 35);
            _cartGlow = AddQuad(_cart, "Lantern", glowMaterial, new Vector3(0.86f, 0.86f, 1f), new Vector3(-0.08f, 0.58f, 0.005f), 36);

            SetCartColor(_cartHull, ProjectCMainMenuPalette.VeilDark, 0.60f);
            SetCartColor(_cartCabin, ProjectCMainMenuPalette.VeilMid, 0.58f);
            SetCartColor(_cartRim, ProjectCMainMenuPalette.Accent, 0.50f);
            SetCartColor(_cartMast, ProjectCMainMenuPalette.VeilDark, 0.66f);
            SetCartColor(_cartPulley, ProjectCMainMenuPalette.VeilDark, 0.66f);
            SetCartColor(_cartGlow, ProjectCMainMenuPalette.GasAccent, 0.50f);
        }

        private static Renderer AddQuad(Transform parent, string name, Material material, Vector3 size, Vector3 localPos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;

            var mesh = new Mesh { name = "PCMM_Quad_" + name };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void SetCartColor(Renderer renderer, Color color, float opacity)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(_cartBlock);
            _cartBlock.SetColor("_Color", color);
            _cartBlock.SetFloat("_Opacity", opacity);
            renderer.SetPropertyBlock(_cartBlock);
        }

        /// <summary>The atmosphere camera only sees its own layer, so the ropes and the
        /// ferry must live on that layer too.</summary>
        public void ApplyLayer(int layer)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        public void Resize(float width, float height)
        {
            _width = Mathf.Max(0.2f, width);
            _height = Mathf.Max(0.2f, height);
            LayoutCart(_depth);
        }

        private void LayoutCart(float depth)
        {
            _depth = Mathf.Clamp(depth, 0f, 1f);
            var frac = Mathf.Lerp(0.082f, 0.026f, _depth);
            CartSize = frac;
            _scale = Mathf.Max(0.0001f, frac * _height / 2f);
        }

        public void Tick(float dt, float sway, float cartSpeed, float depth, float opacityScale)
        {
            if (!_built) Initialize();

            var step = Mathf.Clamp(dt, 0f, 0.05f);
            var swayAmount = Mathf.Clamp(sway, 0f, 2.5f);
            var speed = Mathf.Clamp(cartSpeed, 0f, 4f);
            var depthAmount = Mathf.Clamp(depth, 0f, 1f);
            var opacity = Mathf.Clamp(opacityScale, 0f, 2f);

            _time += step;
            CartSway = swayAmount;

            if (!Mathf.Approximately(depthAmount, _depth))
            {
                LayoutCart(depthAmount);
                if (_cart != null) _cart.localScale = new Vector3(_scale * _face, _scale, _scale);
            }

            for (var r = 0; r < _ropes.Length; r++)
            {
                var rope = _ropes[r];
                if (rope == null) continue;

                var x0 = -0.62f * _width;
                var x1 = 0.62f * _width;
                var cy = rope.Yc * _height;
                var halfW = rope.Width * _height;
                var amp = 0.05f * _height * swayAmount * rope.Sway;

                for (var s = 0; s <= Segments; s++)
                {
                    var u = (float)s / Segments;
                    var sin = Mathf.Sin(Mathf.PI * u);
                    // sin(pi) is a tiny NEGATIVE float at u = 1, and Pow(<0>, 1.12) returns NaN.
                    // One NaN vertex poisons the whole mesh bounds ("Invalid worldAABB" flood).
                    var arc = Mathf.Max(0f, sin);
                    var x = Mathf.Lerp(x0, x1, u)
                            + 0.012f * _width * Mathf.Sin(_time * 0.55f + rope.Phase + u * 3.1f) * sin * swayAmount;
                    var y = cy
                            - rope.Sag * _height * Mathf.Pow(arc, 1.12f)
                            + amp * Mathf.Sin(_time * 0.95f + rope.Phase + u * 2.3f) * sin;
                    rope.Samples[s] = new Vector2(x, y);
                }

                for (var s = 0; s <= Segments; s++)
                {
                    var i0 = Mathf.Max(0, s - 1);
                    var i1 = Mathf.Min(Segments, s + 1);
                    var tx = rope.Samples[i1].x - rope.Samples[i0].x;
                    var ty = rope.Samples[i1].y - rope.Samples[i0].y;
                    var len = Mathf.Sqrt(tx * tx + ty * ty);
                    if (len < 1e-5f) len = 1f;
                    var nx = -ty / len;
                    var ny = tx / len;
                    var px = rope.Samples[s].x;
                    var py = rope.Samples[s].y;
                    rope.Verts[s * 2] = new Vector3(px + nx * halfW, py + ny * halfW, 0f);
                    rope.Verts[s * 2 + 1] = new Vector3(px - nx * halfW, py - ny * halfW, 0f);
                }

                rope.Mesh.vertices = rope.Verts;
                rope.Mesh.RecalculateBounds();

                rope.Renderer.GetPropertyBlock(rope.Block);
                rope.Block.SetColor("_Color", ProjectCMainMenuPalette.VeilDark);
                rope.Block.SetColor("_Hi", ProjectCMainMenuPalette.VeilEdge);
                rope.Block.SetFloat("_Opacity", rope.Opacity * opacity);
                rope.Renderer.SetPropertyBlock(rope.Block);
            }

            // ---- the ferry: slow, lingering at each end -------------------------
            _phase += step * (0.05f + 0.04f * speed);
            var c = Mathf.Cos(_phase);
            var eased = (c < 0f ? -1f : 1f) * Mathf.Pow(Mathf.Abs(c), 1.85f);
            var u01 = 0.5f - 0.45f * eased;

            var lowest = _ropes[_ropes.Length - 1] ?? _ropes[0];
            if (lowest == null || _cart == null) return;

            RopeMidY = SampleRope(lowest, 0.5f).y;

            var here = SampleRope(lowest, u01);
            var ahead = SampleRope(lowest, Mathf.Clamp01(u01 + 0.02f));
            var behind = SampleRope(lowest, Mathf.Clamp01(u01 - 0.02f));
            var slope = Mathf.Atan2(ahead.y - behind.y, (ahead.x - behind.x) == 0f ? 0.0001f : ahead.x - behind.x);

            if (_cart != null)
            {
                _cart.localPosition = new Vector3(here.x, here.y - 1.36f * _scale - 0.012f * _height * depthAmount, 0.9955f);
            }
            var pendulum = Mathf.Sin(_time * 0.62f + _phase * 0.8f) * 0.014f * swayAmount;
            var targetTilt = Mathf.Clamp(slope, -0.13f, 0.13f) + pendulum;
            _tilt = Mathf.Lerp(_tilt, targetTilt, 1f - Mathf.Exp(-2.1f * Mathf.Max(step, 0f)));
            _cart.localRotation = Quaternion.Euler(0f, 0f, _tilt * Mathf.Rad2Deg);

            var dir = Mathf.Sin(_phase) >= 0f ? 1f : -1f;
            CartDir = dir;
            _face = Mathf.Lerp(_face, dir, 1f - Mathf.Exp(-9f * Mathf.Max(step, 0f)));
            if (Mathf.Abs(_face - dir) < 0.004f) _face = dir;
            _cart.localScale = new Vector3(_scale * _face, _scale, _scale);

            var nextX = Mathf.Clamp01(here.x / _width + 0.5f);
            var stepX = _cartStarted ? Mathf.Abs(nextX - CartX) : 0f;
            _cartStarted = true;
            CartX = nextX;
            _lastStep = stepX;

            var edge = Mathf.Clamp01((u01 < 0.5f ? u01 : 1f - u01) / 0.14f);
            var fade = edge * edge * (3f - 2f * edge);
            var alpha = fade * (1f - 0.5f * depthAmount);

            SetCartAlpha(alpha);
            _cart.gameObject.SetActive(fade > 0.02f);
        }

        private void SetCartAlpha(float alpha)
        {
            SetCartColor(_cartHull, ProjectCMainMenuPalette.VeilDark, 0.60f * alpha);
            SetCartColor(_cartCabin, ProjectCMainMenuPalette.VeilMid, 0.58f * alpha);
            SetCartColor(_cartRim, ProjectCMainMenuPalette.Accent, 0.50f * alpha);
            SetCartColor(_cartMast, ProjectCMainMenuPalette.VeilDark, 0.66f * alpha);
            SetCartColor(_cartPulley, ProjectCMainMenuPalette.VeilDark, 0.66f * alpha);
            SetCartColor(_cartGlow, ProjectCMainMenuPalette.GasAccent, 0.50f * alpha * (1f - 0.55f * _depth));
        }

        private static Vector2 SampleRope(Rope rope, float u)
        {
            var f = Mathf.Clamp01(u) * Segments;
            var i0 = Mathf.FloorToInt(f);
            var i1 = Mathf.Min(Segments, i0 + 1);
            var t = f - i0;
            var a = rope.Samples[i0];
            var b = rope.Samples[i1];
            return new Vector2(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t));
        }
    }
}
