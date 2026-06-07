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
            // T-Q11c-fix: struct value semantics — local var для serialize, обратно в поля на READ.
            var tree = treeId;
            var node = nodeId;
            var npc = speakerNpcId;
            var text = speakerText;
            if (s.IsWriter) { tree = treeId ?? ""; node = nodeId ?? ""; npc = speakerNpcId ?? ""; text = speakerText ?? ""; }
            s.SerializeValue(ref tree);
            s.SerializeValue(ref node);
            s.SerializeValue(ref npc);
            s.SerializeValue(ref text);
            if (s.IsReader) { treeId = tree ?? ""; nodeId = node ?? ""; speakerNpcId = npc ?? ""; speakerText = text ?? ""; }
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
            // T-Q11c-fix: в struct ref-поля readonly в serialization, поэтому пишем через
            // local var. На READ side: после SerializeValue local var содержит значение,
            // но this.* НЕ обновляется (struct value semantics). Присваиваем обратно.
            var lbl = label;
            var reason = unavailableReason;
            if (s.IsWriter) { lbl = label ?? ""; reason = unavailableReason ?? ""; }
            s.SerializeValue(ref lbl);
            s.SerializeValue(ref reason);
            // Read: сохраняем прочитанное обратно в struct fields
            if (s.IsReader) { label = lbl ?? ""; unavailableReason = reason ?? ""; }
            s.SerializeValue(ref index);
            s.SerializeValue(ref available);
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
            // T-Q11c-fix: struct value semantics — local var для serialize, обратно в поля на READ.
            var err = errorMessage;
            var dat = resultData;
            if (s.IsWriter) { err = errorMessage ?? ""; dat = resultData ?? ""; }
            s.SerializeValue(ref err);
            s.SerializeValue(ref dat);
            if (s.IsReader) { errorMessage = err ?? ""; resultData = dat ?? ""; }
            s.SerializeValue(ref actionType);
            s.SerializeValue(ref success);
        }
    }
}
