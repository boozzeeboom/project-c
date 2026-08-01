// Project C: Crafting — T-KNOWLEDGE-V3
// RecipeKnowledgeDto: server -> client sync payload for known recipe IDs (string keys).
// INetworkSerializable struct. Design: docs/Character/Knowledges/07_KNOWLEDGE_SYSTEM_V3_INTEGRATION_PLAN.md

using System;
using Unity.Netcode;

namespace ProjectC.Crafting.Dto
{
    [Serializable]
    public struct RecipeKnowledgeDto : INetworkSerializable, IEquatable<RecipeKnowledgeDto>
    {
        public string[] knownRecipeIds;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int len = knownRecipeIds?.Length ?? 0;
            serializer.SerializeValue(ref len);
            if (serializer.IsReader)
            {
                knownRecipeIds = new string[len];
            }
            for (int i = 0; i < len; i++)
            {
                string val = knownRecipeIds[i] ?? "";
                serializer.SerializeValue(ref val);
                if (serializer.IsReader)
                    knownRecipeIds[i] = string.IsNullOrEmpty(val) ? null : val;
            }
        }

        public bool Equals(RecipeKnowledgeDto other)
        {
            if ((knownRecipeIds == null) != (other.knownRecipeIds == null)) return false;
            if (knownRecipeIds == null) return true;
            if (knownRecipeIds.Length != other.knownRecipeIds.Length) return false;
            for (int i = 0; i < knownRecipeIds.Length; i++)
            {
                if (knownRecipeIds[i] != other.knownRecipeIds[i]) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is RecipeKnowledgeDto o && Equals(o);
        public override int GetHashCode() => knownRecipeIds?.Length ?? 0;
    }
}