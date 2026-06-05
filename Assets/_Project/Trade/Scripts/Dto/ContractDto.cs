using Unity.Netcode;

namespace ProjectC.Trade.Dto
{
    /// <summary>
    /// Сетевая DTO одного контракта. Передаётся клиенту в составе
    /// <see cref="ContractSnapshotDto"/> и <see cref="ContractResultDto"/>.
    ///
    /// Серверная истина хранится в <c>ProjectC.Trade.Core.ContractData</c>
    /// (POCO, используется в ContractWorld для бизнес-логики).
    /// Этот DTO — projection layer для сети: содержит всё, что UI нужно
    /// для отображения контракта (displayName заполняется при сериализации
    /// из TradeItemDefinition.displayName, чтобы клиент не дёргал
    /// AssetDatabase/Resources).
    ///
    /// C2-этап миграции контрактов на v2-архитектуру (см. docs/dev/CONTRACT_V2_MIGRATION.md).
    /// </summary>
    public struct ContractDto : INetworkSerializable
    {
        /// <summary>Уникальный ID: contract_{fromLocation}_{itemId}_{index}.</summary>
        public string contractId;

        /// <summary>Тип контракта (Standard / Urgent / Receipt). См. <see cref="ProjectC.Trade.ContractType"/>.</summary>
        public byte type;

        /// <summary>Состояние контракта (Pending / Active / Completed / Failed). См. <see cref="ProjectC.Trade.ContractState"/>.</summary>
        public byte state;

        /// <summary>ID товара (TradeItemDefinition.itemId).</summary>
        public string itemId;

        /// <summary>Кэшированное displayName товара — клиент не дёргает БД.</summary>
        public string displayName;

        /// <summary>Количество единиц товара.</summary>
        public int quantity;

        /// <summary>ID локации отправления (primium/secundus/tertius/quartus).</summary>
        public string fromLocationId;

        /// <summary>ID локации назначения.</summary>
        public string toLocationId;

        /// <summary>Награда за выполнение (CR).</summary>
        public float reward;

        /// <summary>Стоимость груза (basePrice × quantity) — для Receipt контракта.</summary>
        public float cargoValue;

        /// <summary>Лимит времени в секундах (0 = без лимита).</summary>
        public float timeLimit;

        /// <summary>Оставшееся время в секундах (тикает на сервере).</summary>
        public float timeRemaining;

        /// <summary>Это контракт «под расписку»? Товар бесплатно, не доставил = долг ×1.5.</summary>
        public bool isReceiptContract;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref contractId);
            serializer.SerializeValue(ref type);
            serializer.SerializeValue(ref state);
            serializer.SerializeValue(ref itemId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref fromLocationId);
            serializer.SerializeValue(ref toLocationId);
            serializer.SerializeValue(ref reward);
            serializer.SerializeValue(ref cargoValue);
            serializer.SerializeValue(ref timeLimit);
            serializer.SerializeValue(ref timeRemaining);
            serializer.SerializeValue(ref isReceiptContract);
        }
    }
}
