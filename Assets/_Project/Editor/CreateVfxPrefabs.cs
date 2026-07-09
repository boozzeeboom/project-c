// Project C: Skills VFX — Phase 2
// CreateVfxPrefabs: Editor-скрипт для создания примитивных VFX-префабов.
// Запустить: меню Project C > VFX > Create Primitive VFX Prefabs
//
// Создаёт:
//   - PF_VFX_MuzzleFlash_Basic.prefab  — короткая вспышка (ParticleSystem burst)
//   - PF_VFX_Impact_Melee.prefab       — искры при ударе
//   - PF_VFX_Impact_Explosion.prefab   — сфера взрыва
//   - PF_VFX_Projectile_Arrow.prefab   — снаряд-стрела

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectC.Editor.Vfx
{
    public static class CreateVfxPrefabs
    {
        private const string VfxFolder = "Assets/_Project/Resources/Vfx";

        [MenuItem("Project C/VFX/Create Primitive VFX Prefabs")]
        public static void Execute()
        {
            // Создать папку
            if (!AssetDatabase.IsValidFolder(VfxFolder))
            {
                var parent = "Assets/_Project/Resources";
                AssetDatabase.CreateFolder(parent, "Vfx");
            }

            CreateMuzzleFlash();
            CreateImpactMelee();
            CreateImpactExplosion();
            CreateProjectileArrow();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CreateVfxPrefabs] 4 primitive VFX prefabs created in Resources/Vfx/");
        }

        private static void CreateMuzzleFlash()
        {
            var go = new GameObject("PF_VFX_MuzzleFlash_Basic");

            // ParticleSystem: короткий burst, яркий, направленный вперёд
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.15f;
            main.loop = false;
            main.startLifetime = 0.12f;
            main.startSpeed = 3f;
            main.startSize = 0.3f;
            main.startColor = new Color(1f, 0.9f, 0.4f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.05f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            // Renderer: default material
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            SavePrefab(go, "PF_VFX_MuzzleFlash_Basic");
        }

        private static void CreateImpactMelee()
        {
            var go = new GameObject("PF_VFX_Impact_Melee");

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = 0.18f;
            main.startSpeed = 2.5f;
            main.startSize = 0.15f;
            main.startColor = new Color(1f, 0.7f, 0.2f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.5f, 0.5f), new Keyframe(1f, 0f)));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                new Gradient()
                {
                    colorKeys = new[]
                    {
                        new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                        new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0.5f),
                        new GradientColorKey(new Color(0.2f, 0.1f, 0.05f), 1f)
                    },
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.3f, 1f)
                    }
                });

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            SavePrefab(go, "PF_VFX_Impact_Melee");
        }

        private static void CreateImpactExplosion()
        {
            var go = new GameObject("PF_VFX_Impact_Explosion");

            // Основной взрыв: сфера расширяющихся частиц
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 6f;
            main.startSize = 0.6f;
            main.startColor = new Color(1f, 0.5f, 0.1f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
                new Gradient()
                {
                    colorKeys = new[]
                    {
                        new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0f),
                        new GradientColorKey(new Color(1f, 0.3f, 0.05f), 0.3f),
                        new GradientColorKey(new Color(0.3f, 0.1f, 0f), 1f)
                    },
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.1f, 1f)
                    }
                });

            // Второй ParticleSystem: дым
            var smokeGo = new GameObject("Smoke");
            smokeGo.transform.SetParent(go.transform);
            var smokePs = smokeGo.AddComponent<ParticleSystem>();
            var smokeMain = smokePs.main;
            smokeMain.duration = 0.8f;
            smokeMain.loop = false;
            smokeMain.startLifetime = 0.7f;
            smokeMain.startSpeed = 1.5f;
            smokeMain.startSize = 0.4f;
            smokeMain.startColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            smokeMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var smokeEmission = smokePs.emission;
            smokeEmission.rateOverTime = 0f;
            smokeEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

            var smokeShape = smokePs.shape;
            smokeShape.shapeType = ParticleSystemShapeType.Sphere;
            smokeShape.radius = 0.3f;

            var smokeRenderer = smokeGo.GetComponent<ParticleSystemRenderer>();
            smokeRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            SavePrefab(go, "PF_VFX_Impact_Explosion");
        }

        private static void CreateProjectileArrow()
        {
            var go = new GameObject("PF_VFX_Projectile_Arrow");

            // Trail (ParticleSystem с растянутыми частицами)
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f;        // долгий — управляется через скрипт
            main.loop = true;
            main.startLifetime = 0.15f;
            main.startSpeed = 0.1f;
            main.startSize = 0.08f;
            main.startColor = new Color(1f, 0.9f, 0.3f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 20f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 0.5f;
            renderer.velocityScale = 0.3f;

            SavePrefab(go, "PF_VFX_Projectile_Arrow");
        }

        private static void SavePrefab(GameObject go, string name)
        {
            string path = $"{VfxFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log($"[CreateVfxPrefabs] Created: {path}");
        }
    }
}
#endif
