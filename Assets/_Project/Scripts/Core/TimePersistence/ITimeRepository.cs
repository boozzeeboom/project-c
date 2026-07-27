namespace ProjectC.Core.TimePersistence
{
    /// <summary>
    /// Persistence contract for game time state.
    /// </summary>
    public interface ITimeRepository
    {
        void Save(GameTimeData data, float timeOfDay);
        bool TryLoad(out GameTimeData data, out float timeOfDay);
    }
}
