using System.Collections.Generic;
using UnityEngine;
using ProjectC.Ship;

namespace ProjectC.World.Core
{
    /// <summary>
    /// Горный массив — группа вершин, соединённых хребтами.
    /// Создаётся через Unity Editor: Create → Project C → Mountain Massif
    /// </summary>
    [CreateAssetMenu(menuName = "Project C/Mountain Massif", fileName = "MountainMassif")]
    public class MountainMassif : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный ID массива (himalayan, alpine, african, andean, alaskan)")]
        public string massifId;

        [Tooltip("Отображаемое имя")]
        public string displayName;

        [Header("Центр массива")]
        [Tooltip("Координаты главного города")]
        public Vector3 centerPosition;

        [Tooltip("Радиус влияния массива (units)")]
        public float massifRadius;

        [Header("Климатический профиль")]
        [Tooltip("ScriptableObject с цветами и атмосферой биома")]
        public BiomeProfile biomeProfile;

        [Header("Пики")]
        [Tooltip("Список всех пиков массива")]
        public List<PeakData> peaks = new List<PeakData>();

        [Header("Хребты")]
        [Tooltip("Список хребтов, соединяющих пики")]
        public List<RidgeData> ridges = new List<RidgeData>();

        [Header("Фермерские угодья")]
        [Tooltip("Список ферм на территории массива")]
        public List<FarmData> farms = new List<FarmData>();

        [Header("Городской коридор")]
        [Tooltip("AltitudeCorridorData для главного города массива")]
        public AltitudeCorridorData cityCorridor;

        /// <summary>
        /// Найти пик по ID.
        /// </summary>
        public PeakData FindPeakById(string peakId)
        {
            return peaks.Find(p => p.peakId == peakId);
        }

        /// <summary>
        /// Найти ферму по ID.
        /// </summary>
        public FarmData FindFarmById(string farmId)
        {
            return farms.Find(f => f.farmId == farmId);
        }
    }
}
