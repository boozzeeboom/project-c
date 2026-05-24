using UnityEngine;

namespace ProjectC.Core
{
    [CreateAssetMenu(fileName = "NewConstellationData", menuName = "ProjectC/DayNight/ConstellationData")]
    public class ConstellationData : ScriptableObject
    {
        [System.Serializable]
        public class Star
        {
            public string name;
            public Vector2 sphericalPosition;
            public float magnitude;
            public float temperature;
        }
        
        [System.Serializable]
        public class Constellation
        {
            public string constellationName;
            public string localizedName;
            public Star[] stars;
            public int[] linePairs;
            public bool isNavigable;
            public string hemisphere;
        }
        
        public Constellation[] constellations = new Constellation[0];
    }
}
