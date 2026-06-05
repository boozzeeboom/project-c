using Unity.Netcode;

namespace ProjectC.Trade.Dto
{
    /// <summary>
    /// Результат одной контрактной операции (accept / complete / fail).
    /// Сервер шлёт клиенту после обработки RPC.
    ///
    /// Содержит:
    ///   • code + success — статус операции (для UI feedback)
    ///   • message        — локализованное сообщение (для UI message-label)
    ///   • reward         — награда (для complete; 0 в остальных)
    ///   • newCredits     — обновлённые кредиты игрока (для HUD)
    ///   • newDebt        — обновлённый долг (для UI debt-label)
    ///   • updatedContract — обновлённый контракт (если был accept; иначе null)
    ///   • newSnapshot    — полный re-snapshot (опционально; обычно null, клиент сам запросит)
    ///
    /// C2-этап миграции контрактов на v2-архитектуру (см. docs/dev/CONTRACT_V2_MIGRATION.md).
    /// </summary>
    public struct ContractResultDto : INetworkSerializable
    {
        /// <summary>Код результата (см. <see cref="ContractResultCode"/>).</summary>
        public byte code;

        /// <summary>ID контракта, к которому относится результат (для UI выделения строки).</summary>
        public string contractId;

        /// <summary>Успешна ли операция (code == Ok).</summary>
        public bool success;

        /// <summary>Локализованное сообщение для UI (например: "Контракт принят: [Стандарт]").</summary>
        public string message;

        /// <summary>Награда в CR (для complete; 0 в остальных).</summary>
        public float reward;

        /// <summary>Обновлённые кредиты игрока (для синхронизации HUD).</summary>
        public float newCredits;

        /// <summary>Обновлённый долг игрока (для UI debt-label).</summary>
        public float newDebt;

        /// <summary>
        /// Обновлённый контракт (например, после accept — state перешёл в Active).
        /// Может быть null, если операция не изменила состояние контракта.
        /// </summary>
        public ContractDto? updatedContract;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref code);
            serializer.SerializeValue(ref contractId);
            serializer.SerializeValue(ref success);
            serializer.SerializeValue(ref message);
            serializer.SerializeValue(ref reward);
            serializer.SerializeValue(ref newCredits);
            serializer.SerializeValue(ref newDebt);

            // nullable updatedContract — флаг + значение
            // FIX (2026-06-05): на reader-пути updatedContract == default (null),
            // и вызов .Value на нём бросает InvalidOperationException.
            // Решение: на writer-пути читаем через .Value (там оно точно есть);
            // на reader-пути — используем локальную переменную c = default, без
            // обращения к .Value. Запись обратно в updatedContract — только после
            // успешной десериализации.
            if (serializer.IsWriter)
            {
                bool hasContract = updatedContract.HasValue;
                serializer.SerializeValue(ref hasContract);
                if (hasContract)
                {
                    var c = updatedContract.Value;
                    c.NetworkSerialize(serializer);
                }
            }
            else
            {
                bool hasContract = false;
                serializer.SerializeValue(ref hasContract);
                if (hasContract)
                {
                    var c = default(ContractDto);
                    c.NetworkSerialize(serializer);
                    updatedContract = c;
                }
                else
                {
                    updatedContract = null;
                }
            }
        }

        /// <summary>Удобный helper для UI: code == Ok ?.</summary>
        public bool IsSuccess => code == (byte)ContractResultCode.Ok;
    }
}
