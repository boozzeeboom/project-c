// T-Q10: DialogStepDto + DialogOptionDto — server → client payload для active dialog step.
// Pattern: ContractDto/ContractResultDto (Trade). Lightweight, INetworkSerializable.

using Unity.Netcode;

namespace ProjectC.Quests.Dto
{
    public struct DialogStepDto : INetworkSerializable
    {
        public string treeId;
        public string nodeId;
        public string speakerNpcId;     // ref into QuestDatabase.npcs (для UI: portrait/name)
        public string speakerText;      // localized text (T-Q10: plain string, T-Q18: localization key)
        public DialogOptionDto[] options; // empty = dialog end
        public bool isEnd;              // true → close dialog window

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref treeId);
            s.SerializeValue(ref nodeId);
            s.SerializeValue(ref speakerNpcId);
            s.SerializeValue(ref speakerText);
            s.SerializeValue(ref isEnd);
            SerializeOptions(ref options, s);
        }

        private static void SerializeOptions<T>(ref DialogOptionDto[] arr, BufferSerializer<T> s) where T : IReaderWriter
        {
            int len = arr?.Length ?? 0;
            s.SerializeValue(ref len);
            if (s.IsReader) arr = len > 0 ? new DialogOptionDto[len] : null;
            for (int i = 0; i < len; i++)
            {
                var item = arr != null && i < arr.Length ? arr[i] : default;
                item.NetworkSerialize(s);
                if (arr != null) arr[i] = item;
            }
        }
    }

    public struct DialogOptionDto : INetworkSerializable
    {
        public int index;             // server-side option index
        public string label;          // button text
        public bool available;        // false = grayed (conditions not met)
        public string unavailableReason; // short RU hint (e.g. "Нужен ключ")

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref index);
            s.SerializeValue(ref label);
            s.SerializeValue(ref available);
            s.SerializeValue(ref unavailableReason);
        }
    }

    public struct DialogActionResultDto : INetworkSerializable
    {
        public byte actionType;     // DialogueActionType enum
        public bool success;
        public string errorMessage;
        public string resultData;   // optional (e.g. questId offered)

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref actionType);
            s.SerializeValue(ref success);
            s.SerializeValue(ref errorMessage);
            s.SerializeValue(ref resultData);
        }
    }
}
