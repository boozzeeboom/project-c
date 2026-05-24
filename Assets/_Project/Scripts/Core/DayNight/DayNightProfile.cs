using UnityEngine;

namespace ProjectC.Core
{
    [CreateAssetMenu(fileName = "NewDayNightProfile", menuName = "ProjectC/DayNight/DayNightProfile")]
    public class DayNightProfile : ScriptableObject
    {
        public TimeOfDayPhase[] phases = new TimeOfDayPhase[5];
    }
}
