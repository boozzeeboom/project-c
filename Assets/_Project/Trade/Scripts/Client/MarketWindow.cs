namespace ProjectC.Trade.Client
{
    /// <summary>
    /// Совместимый facade старого MarketWindow.
    /// Реальная логика находится в MarketWindowHost и tab controllers.
    /// Сохранение публичного имени и Instance не требует изменений в сценах
    /// и MarketInteractor.
    /// </summary>
    public sealed class MarketWindow : MarketWindowHost
    {
        public static MarketWindow Instance { get; private set; }

        protected override void AwakeWindow()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                DisableLifecycle();
                Destroy(gameObject);
            }
        }

        protected override void DestroyWindow()
        {
            if (Instance == this) Instance = null;
        }
    }
}
