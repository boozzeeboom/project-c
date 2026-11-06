using UnityEngine;

namespace ProjectC.World
{
    /// <summary>
    /// Маркер дома/фермы для карты и discovery.
    /// Создаётся Editor-утилитой (или runtime-парсером) под корнем города/чанка.
    /// </summary>
    [DisallowMultipleComponent]
    public class HouseMarker : MonoBehaviour
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный ID дома, например N2-101 или Farm 0-2")]
        public string houseId = "";

        [Tooltip("Номер дома внутри чанка (как в имени)")]
        public int number = 0;

        [Tooltip("ID чанка/аллеи, например S2_TRD")]
        public string chunkId = "";

        [Tooltip("Город/поселение: Primum, Farm, Secund, ...")]
        public string city = "Primum";

        [Header("Карта")]
        [Tooltip("Точка для карты (центр/вход). Если не задана — transform.position")]
        public Vector3 mapPoint = Vector3.zero;
        public bool hasMapPoint = false;

        public Vector3 MapPosition => hasMapPoint ? mapPoint : transform.position;

        /// <summary>Название для отображения на карте/табличке.</summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(houseId)) return houseId;
                return gameObject.name;
            }
        }
    }
}
