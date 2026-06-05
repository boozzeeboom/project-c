using Unity.Netcode;

namespace ProjectC.Trade.Dto
{
    /// <summary>
    /// Снепшот состояния контрактной доски NPC-агента НП для конкретной локации.
    /// Сервер шлёт клиенту в ответ на <c>ContractServer.RequestListRpc</c>
    /// и после каждой успешной операции (accept/complete/fail).
    ///
    /// Содержит:
    ///   • available[] — pending контракты на этой локации (для UI вкладки «ДОСТУПНЫЕ»)
    ///   • active[]    — активные контракты игрока (для UI вкладки «МОИ КОНТРАКТЫ»)
    ///   • debt info   — текущий долг игрока (для UI-предупреждения)
    ///   • time info   — для UI-таймера обратного отсчёта
    ///
    /// C2-этап миграции контрактов на v2-архитектуру (см. docs/dev/CONTRACT_V2_MIGRATION.md).
    /// </summary>
    public struct ContractSnapshotDto : INetworkSerializable
    {
        /// <summary>ID локации (primium/secundus/tertius/quartus).</summary>
        public string locationId;

        /// <summary>Отображаемое имя локации (для UI заголовка).</summary>
        public string displayName;

        /// <summary>Pending контракты (готовы к принятию) на этой локации.</summary>
        public ContractDto[] available;

        /// <summary>Активные контракты игрока (state == Active).</summary>
        public ContractDto[] active;

        /// <summary>Текущий долг игрока (CR). 0 = нет долга.</summary>
        public float debtAmount;

        /// <summary>Уровень долга (см. <c>ProjectC.Trade.DebtLevel</c>). 0=None, 1=Warning, 2=Restricted, 3=Hunted, 4=Bounty, 5=Headhunt.</summary>
        public int debtLevel;

        /// <summary>Может ли игрок брать новые контракты (false если долг &gt;= Restricted).</summary>
        public bool canAcceptContracts;

        /// <summary>Множитель времени рынка (для UI-таймера). 1.0 = реальное время.</summary>
        public float marketTimeMultiplier;

        /// <summary>Секунд до следующего тика (для UI обратного отсчёта).</summary>
        public float secondsUntilNextTick;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref locationId);
            serializer.SerializeValue(ref displayName);
            serializer.SerializeValue(ref debtAmount);
            serializer.SerializeValue(ref debtLevel);
            serializer.SerializeValue(ref canAcceptContracts);
            serializer.SerializeValue(ref marketTimeMultiplier);
            serializer.SerializeValue(ref secondsUntilNextTick);

            // массивы ContractDto
            SerializeArray<T>(ref available, serializer);
            SerializeArray<T>(ref active, serializer);
        }

        private static void SerializeArray<T>(ref ContractDto[] arr, BufferSerializer<T> s)
            where T : IReaderWriter
        {
            int len = arr?.Length ?? 0;
            s.SerializeValue(ref len);
            if (s.IsReader)
            {
                arr = len > 0 ? new ContractDto[len] : null;
            }
            for (int i = 0; i < len; i++)
            {
                var item = arr[i];
                item.NetworkSerialize(s);
                arr[i] = item;
            }
        }
    }
}
