// Project C: Skills VFX — Phase 1
// ParticleSystemVfxProvider: реализация ISkillVfxProvider через ParticleSystem/GameObject.
// Обрабатывает cast, projectile и impact VFX.
//
// Projectile использует ручную интерполяцию (аналогично текущим ThrowArcVisual/ProjectileVisual),
// но спавнит префаб из SkillNodeConfig.projectileVfxPrefab вместо примитивов.
//
// 2D-ready: код не хардкодит ParticleSystem — работает с GameObject + опциональный VfxInstance.

using System.Collections;
using ProjectC.Combat.Core;
using UnityEngine;

namespace ProjectC.Skills.Vfx
{
    /// <summary>
    /// VFX-провайдер на ParticleSystem/GameObject. Спавнит префабы из SkillNodeConfig.
    /// </summary>
    public class ParticleSystemVfxProvider : ISkillVfxProvider
    {
        private readonly VfxObjectPool _pool;
        private readonly MonoBehaviour _coroutineOwner;

        public ParticleSystemVfxProvider(VfxObjectPool pool, MonoBehaviour coroutineOwner)
        {
            _pool = pool;
            _coroutineOwner = coroutineOwner;
        }

        // ==================== Cast VFX ====================

        public void PlayCastVfx(SkillNodeConfig config, Transform character)
        {
            if (config == null || config.castVfxPrefab == null || character == null) return;

            Vector3 spawnPos = VfxBoneResolver.Resolve(character, config.castSpawnPoint);
            Quaternion spawnRot = character.rotation;

            if (config.castVfxDelay > 0f)
            {
                _coroutineOwner.StartCoroutine(DelayedCast(config, spawnPos, spawnRot));
            }
            else
            {
                SpawnAndAutoDestroy(config.castVfxPrefab, spawnPos, spawnRot, config.castVfxDuration);
            }
        }

        private IEnumerator DelayedCast(SkillNodeConfig config, Vector3 pos, Quaternion rot)
        {
            yield return new WaitForSeconds(config.castVfxDelay);
            SpawnAndAutoDestroy(config.castVfxPrefab, pos, rot, config.castVfxDuration);
        }

        // ==================== Projectile VFX ====================

        public void PlayProjectileVfx(SkillNodeConfig config, Vector3 from, Vector3 to, System.Action onArrived)
        {
            if (config == null) return;

            // Если есть префаб — спавним его. Иначе — fallback на примитив (MVP).
            if (config.projectileVfxPrefab != null)
            {
                _coroutineOwner.StartCoroutine(ProjectileRoutine(config, from, to, onArrived));
            }
            else
            {
                // Fallback: примитив как в текущих ThrowArcVisual/ProjectileVisual.
                _coroutineOwner.StartCoroutine(PrimitiveProjectileRoutine(config, from, to, onArrived));
            }
        }

        private IEnumerator ProjectileRoutine(SkillNodeConfig config, Vector3 from, Vector3 to, System.Action onArrived)
        {
            var instance = _pool.Get(config.projectileVfxPrefab, from, Quaternion.LookRotation((to - from).normalized));
            if (instance == null) { onArrived?.Invoke(); yield break; }

            float dist = Vector3.Distance(from, to);
            float speed = Mathf.Max(0.1f, config.projectileSpeed);
            float duration = dist / speed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 pos = Vector3.Lerp(from, to, t);

                // Arc (для throwables)
                if (config.projectileArcHeight > 0f)
                    pos.y += Mathf.Sin(t * Mathf.PI) * config.projectileArcHeight;

                instance.transform.position = pos;

                // Поворот по направлению
                if (elapsed + Time.deltaTime < duration)
                {
                    Vector3 nextPos = Vector3.Lerp(from, to, Mathf.Clamp01((elapsed + Time.deltaTime) / duration));
                    if (config.projectileArcHeight > 0f)
                        nextPos.y += Mathf.Sin(Mathf.Clamp01((elapsed + Time.deltaTime) / duration) * Mathf.PI) * config.projectileArcHeight;
                    instance.transform.rotation = Quaternion.LookRotation((nextPos - pos).normalized);
                }

                yield return null;
            }

            _pool.Return(instance);
            onArrived?.Invoke();
        }

        /// <summary>
        /// Fallback: примитивный projectile (сфера + LineRenderer), если префаб не задан.
        /// </summary>
        private IEnumerator PrimitiveProjectileRoutine(SkillNodeConfig config, Vector3 from, Vector3 to, System.Action onArrived)
        {
            var go = new GameObject("PrimitiveProjectile");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(go.transform);
            sphere.transform.localScale = Vector3.one * 0.15f;
            Object.Destroy(sphere.GetComponent<Collider>());

            var lr = go.AddComponent<LineRenderer>();
            lr.startWidth = 0.04f;
            lr.endWidth = 0f;
            lr.material = config.projectileTrailMaterial != null
                ? new Material(config.projectileTrailMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lr.material.color = new Color(1f, 0.85f, 0.3f, 0.6f);

            float dist = Vector3.Distance(from, to);
            float speed = Mathf.Max(0.1f, config.projectileSpeed);
            float duration = dist / speed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Vector3 pos = Vector3.Lerp(from, to, t);
                if (config.projectileArcHeight > 0f)
                    pos.y += Mathf.Sin(t * Mathf.PI) * config.projectileArcHeight;

                go.transform.position = pos;
                lr.positionCount = 2;
                lr.SetPosition(0, pos);
                lr.SetPosition(1, from);
                yield return null;
            }

            Object.Destroy(go);
            onArrived?.Invoke();
        }

        // ==================== Impact VFX ====================

        public void PlayImpactVfx(SkillNodeConfig config, Vector3 position, DamageType damageType, bool isCrit)
        {
            if (config == null || config.impactVfxPrefab == null) return;

            var instance = _pool.Get(config.impactVfxPrefab, position, Quaternion.identity);
            if (instance == null) return;

            // Окраска по типу урона
            if (config.impactColorByDamageType)
            {
                Color c = DamageTypeColors.Get(damageType);
                ApplyColor(instance, c);
            }

            // Масштаб по урону / криту
            float scale = 1f;
            if (config.impactScaleByDamage && isCrit) scale = 1.5f;
            instance.transform.localScale = Vector3.one * scale;

            _coroutineOwner.StartCoroutine(AutoReturn(instance, config.impactVfxDuration));
        }

        // ==================== Helpers ====================

        private void SpawnAndAutoDestroy(GameObject prefab, Vector3 pos, Quaternion rot, float duration)
        {
            var instance = _pool.Get(prefab, pos, rot);
            if (instance != null)
                _coroutineOwner.StartCoroutine(AutoReturn(instance, duration));
        }

        private IEnumerator AutoReturn(GameObject instance, float duration)
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
            _pool.Return(instance);
        }

        private static void ApplyColor(GameObject obj, Color color)
        {
            foreach (var ps in obj.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.startColor = color;
            }
            foreach (var rend in obj.GetComponentsInChildren<Renderer>())
            {
                if (rend.material.HasProperty("_BaseColor"))
                    rend.material.SetColor("_BaseColor", color);
                else if (rend.material.HasProperty("_Color"))
                    rend.material.SetColor("_Color", color);
            }
        }
    }
}
