using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Trade.Core
{
    /// <summary>
    /// Серверный результат торговой операции. НЕ DTO (DTO — это <see cref="Dto.TradeResultDto"/>
    /// для передачи по сети). Это результат, с которым работает <see cref="TradeWorld"/>.
    /// </summary>
    public readonly struct TradeResult
    {
        public readonly TradeResultCode code;
        public readonly string message;
        public readonly float newCredits;
        public readonly int newMarketStock;
        public readonly Warehouse updatedWarehouse;
        public readonly CargoData updatedCargo;

        public bool IsSuccess => code == TradeResultCode.Ok;

        private TradeResult(TradeResultCode code, string message, float newCredits, int newMarketStock,
            Warehouse updatedWarehouse, CargoData updatedCargo)
        {
            this.code = code;
            this.message = message;
            this.newCredits = newCredits;
            this.newMarketStock = newMarketStock;
            this.updatedWarehouse = updatedWarehouse;
            this.updatedCargo = updatedCargo;
        }

        public static TradeResult Ok(float newCredits, int newStock, Warehouse wh, CargoData cargo)
            => new TradeResult(TradeResultCode.Ok, null, newCredits, newStock, wh, cargo);

        public static TradeResult Fail(TradeResultCode code, string message, float currentCredits, Warehouse wh, CargoData cargo)
            => new TradeResult(code, message, currentCredits, 0, wh, cargo);
    }
}
