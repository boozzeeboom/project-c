using UnityEngine;

namespace ProjectC.Core
{
    [CreateAssetMenu(fileName = "NewPhase", menuName = "ProjectC/DayNight/TimeOfDayPhase")]
    public class TimeOfDayPhase : ScriptableObject
    {
        [Header("Identity")]
        public string phaseName = "New Phase";
        public float startHour = 0f;
        public float endHour = 24f;

        [Header("Sun Light")]
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        public float sunTemperature = 5500f;
        public bool castShadows = true;

        [Header("Ambient Light")]
        public Color ambientSkyColor = new Color(0.2f, 0.2f, 0.3f);
        public Color ambientEquatorColor = new Color(0.3f, 0.3f, 0.4f);
        public Color ambientGroundColor = new Color(0.1f, 0.1f, 0.15f);
        public float ambientIntensity = 0.5f;

        [Header("Skybox")]
        public Gradient skyHorizonGradient;
        public float skyboxExposure = 1f;
        public Color skyboxTint = Color.white;

        [Header("Fog")]
        public Color fogColor = Color.gray;
        public float fogDensity = 0.0003f;

        [Header("Variability (randomization ranges)")]
        public Vector2 hueShiftRange = new Vector2(-0.05f, 0.05f);
        public Vector2 saturationRange = new Vector2(0.8f, 1.2f);
        public Vector2 intensityRange = new Vector2(0.85f, 1.15f);

        [Header("Transition")]
        public AnimationCurve transitionCurve = AnimationCurve.Linear(0,0,1,1);
    }
}
