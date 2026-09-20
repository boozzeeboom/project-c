// Project C: Дальность прорисовки — пресеты (T-LOD01)
// Один контрол в ESC → Видео управляет четырьмя кольцами:
// Near (ближнее) / Medium (среднее) / Far (дальнее-фон) / Ultra (резерв, сцен 2+ нет).
namespace ProjectC.Core
{
    /// <summary>
    /// Пресет дальности прорисовки. Хранится в SettingsManager (PlayerPrefs int).
    /// Ultra зарезервирован: UI его не предлагает, апplier сводит к Far (см. Phase 5 в docs/world/optimization).
    /// </summary>
    public enum ViewDistance
    {
        Near = 0,
        Medium = 1,
        Far = 2,
        Ultra = 3
    }
}
