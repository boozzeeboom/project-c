using UnityEngine;

namespace ProjectC.Core
{
    public class MoonSprite : MonoBehaviour
    {
        [Header("References")]
        public Material moonMaterial;

        [Header("Settings")]
        public float distanceFromCamera = 5000f;
        public float moonSize = 100f;
        public float orbitSpeed = 0.5f;

        private float _moonAge = 0f;
        private float _orbitAngle = 210f;
        private const float LUNAR_CYCLE_DAYS = 29.5f;
        private Renderer _renderer;

        void Start()
        {
            if (moonMaterial != null)
            {
                _renderer = GetComponent<Renderer>();
                if (_renderer == null)
                {
                    _renderer = gameObject.AddComponent<MeshRenderer>();
                }
                _renderer.material = moonMaterial;
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
            }

            _moonAge = 0f;
        }

        void Update()
        {
            _moonAge = Mathf.Repeat(_moonAge + Time.deltaTime * (1f / (LUNAR_CYCLE_DAYS * 3600f)), LUNAR_CYCLE_DAYS);
            _orbitAngle = Mathf.Repeat(_orbitAngle + Time.deltaTime * orbitSpeed, 360f);

            PositionMoon();
            UpdateShaderProperties();
        }

        void PositionMoon()
        {
            if (Camera.main == null) return;

            float rad = _orbitAngle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0.3f, Mathf.Cos(rad)).normalized;

            Vector3 targetPos = Camera.main.transform.position + dir * distanceFromCamera;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 2f);

            transform.LookAt(Camera.main.transform);
        }

        void UpdateShaderProperties()
        {
            if (moonMaterial == null) return;

            float phase = _moonAge / LUNAR_CYCLE_DAYS;
            moonMaterial.SetFloat("_MoonPhase", phase);

            float dayOpacity = 1f;
            if (_orbitAngle > 100f && _orbitAngle < 260f)
            {
                dayOpacity = 0f;
            }
            else if (_orbitAngle > 80f && _orbitAngle < 100f)
            {
                dayOpacity = Mathf.InverseLerp(80f, 100f, _orbitAngle);
            }
            else if (_orbitAngle > 260f && _orbitAngle < 280f)
            {
                dayOpacity = Mathf.InverseLerp(280f, 260f, _orbitAngle);
            }

            moonMaterial.SetFloat("_NightVisibility", dayOpacity);
        }

        public void SetMoonPhase(float phase)
        {
            _moonAge = phase * LUNAR_CYCLE_DAYS;
            if (moonMaterial != null)
            {
                moonMaterial.SetFloat("_MoonPhase", phase);
            }
        }
    }
}