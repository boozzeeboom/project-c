// Project C: Character Progression — T-P13
// SkillsSnapshotDto + SkillResultDto: server → client sync payload.
// INetworkSerializable struct. Design: docs/Character/06_SKILL_TREE.md §2.2-§2.3.

using System;
using Unity.Netcode;

namespace ProjectC.Skills.Dto
{
    public enum SkillResultCode : byte
    {
        Learned   = 0,
        Forgotten = 1,
        Denied    = 2,
    }

    [Serializable]
    public struct SkillsSnapshotDto : INetworkSerializable, IEquatable<SkillsSnapshotDto>
    {
        public string[] learnedSkillIds;
        // T-KNOWLEDGE-V2: known (but not learned) skill IDs
        public string[] knownSkillIds;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            SerializeStringArray(serializer, ref learnedSkillIds);
            SerializeStringArray(serializer, ref knownSkillIds);
        }

        private static void SerializeStringArray<T>(BufferSerializer<T> serializer, ref string[] arr) where T : IReaderWriter
        {
            int len = arr?.Length ?? 0;
            serializer.SerializeValue(ref len);
            if (serializer.IsReader)
            {
                arr = new string[len];
            }
            for (int i = 0; i < len; i++)
            {
                string s = arr[i] ?? string.Empty;
                int sLen = s.Length;
                serializer.SerializeValue(ref sLen);
                if (sLen > 0)
                {
                    if (serializer.IsReader)
                    {
                        var bytes = new byte[sLen];
                        for (int j = 0; j < sLen; j++) serializer.SerializeValue(ref bytes[j]);
                        arr[i] = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    else
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
                        for (int j = 0; j < sLen; j++) serializer.SerializeValue(ref bytes[j]);
                    }
                }
                else if (serializer.IsReader)
                {
                    arr[i] = string.Empty;
                }
            }
        }

        public bool Equals(SkillsSnapshotDto other)
        {
            if ((learnedSkillIds == null) != (other.learnedSkillIds == null)) return false;
            if (learnedSkillIds == null) return true;
            if (learnedSkillIds.Length != other.learnedSkillIds.Length) return false;
            for (int i = 0; i < learnedSkillIds.Length; i++)
            {
                if (learnedSkillIds[i] != other.learnedSkillIds[i]) return false;
            }
            // knownSkillIds
            if ((knownSkillIds == null) != (other.knownSkillIds == null)) return false;
            if (knownSkillIds == null) return true;
            if (knownSkillIds.Length != other.knownSkillIds.Length) return false;
            for (int i = 0; i < knownSkillIds.Length; i++)
            {
                if (knownSkillIds[i] != other.knownSkillIds[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is SkillsSnapshotDto o && Equals(o);
        public override int GetHashCode() => (learnedSkillIds?.Length ?? 0) ^ (knownSkillIds?.Length ?? 0);
    }
=======


    [Serializable]
    public struct SkillResultDto : INetworkSerializable, IEquatable<SkillResultDto>
    {
        public SkillResultCode code;
        public string skillId;
        public string reason;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            byte codeByte = (byte)code;
            serializer.SerializeValue(ref codeByte);
            if (serializer.IsReader) code = (SkillResultCode)codeByte;
            SerializeString(serializer, ref skillId);
            SerializeString(serializer, ref reason);
        }

        private static void SerializeString<T>(BufferSerializer<T> serializer, ref string str) where T : IReaderWriter
        {
            string s = str ?? string.Empty;
            int sLen = s.Length;
            serializer.SerializeValue(ref sLen);
            if (sLen > 0)
            {
                if (serializer.IsReader)
                {
                    var bytes = new byte[sLen];
                    for (int j = 0; j < sLen; j++) serializer.SerializeValue(ref bytes[j]);
                    str = System.Text.Encoding.UTF8.GetString(bytes);
                }
                else
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(s);
                    for (int j = 0; j < sLen; j++) serializer.SerializeValue(ref bytes[j]);
                }
            }
            else if (serializer.IsReader)
            {
                str = string.Empty;
            }
        }

        public static SkillResultDto Learned(string skillId) => new SkillResultDto
        {
            code = SkillResultCode.Learned, skillId = skillId ?? string.Empty, reason = string.Empty,
        };
        public static SkillResultDto Forgotten(string skillId) => new SkillResultDto
        {
            code = SkillResultCode.Forgotten, skillId = skillId ?? string.Empty, reason = string.Empty,
        };
        public static SkillResultDto Denied(string skillId, string reason) => new SkillResultDto
        {
            code = SkillResultCode.Denied, skillId = skillId ?? string.Empty, reason = reason ?? string.Empty,
        };

        public bool Equals(SkillResultDto other) => code == other.code && skillId == other.skillId && reason == other.reason;
        public override bool Equals(object obj) => obj is SkillResultDto o && Equals(o);
        public override int GetHashCode() => HashCode.Combine((byte)code, skillId, reason);
    }
}
