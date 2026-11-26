// =====================================================================================
// VariationRandom.cs — детерминированный PRNG для генератора вариаций (T-VAR01)
// =====================================================================================
// Документация: docs/pickble_items/variation_generator/01_DESIGN.md §4 (детерминизм)
//
// Используем собственный SplitMix64 вместо System.Random/UnityEngine.Random:
//   • System.Random не гарантирует одинаковую последовательность между версиями .NET;
//   • UnityEngine.Random зависит от глобального состояния и не годится для сервера;
//   • SplitMix64 — 8 байт состояния, полностью переносим, без float-операций.
//
// Вся генерация (тир, выбор аффиксов, роллы) идёт через целые числа, поэтому один и тот
// же seed + профиль + базовые статы дают бит-в-бит одинаковый результат.
// =====================================================================================

using System;

namespace ProjectC.Items.Variation
{
    public struct DeterministicRandom
    {
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            _state = seed + GoldenGamma;
        }

        public ulong NextULong()
        {
            _state += GoldenGamma;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        /// <summary>[minInclusive, maxExclusive). Пустой диапазон → minInclusive.</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;

            ulong range = (ulong)((long)maxExclusive - minInclusive);
            if (range > uint.MaxValue)
                return (int)(minInclusive + (long)(NextULong() % range));

            // Lemire multiply-shift: без дорогого modulo и без заметного bias.
            ulong product = (ulong)NextUInt() * range;
            return minInclusive + (int)(product >> 32);
        }

        /// <summary>[minInclusive, maxInclusive].</summary>
        public int NextIntInclusive(int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;
            if (maxInclusive == int.MaxValue) return NextInt(minInclusive, int.MaxValue);
            return NextInt(minInclusive, maxInclusive + 1);
        }

        public long NextLong(long maxExclusive)
        {
            if (maxExclusive <= 0L) return 0L;
            return (long)(NextULong() % (ulong)maxExclusive);
        }

        public bool ChanceBps(int bps)
        {
            if (bps <= 0) return false;
            if (bps >= 10000) return true;
            return NextInt(0, 10000) < bps;
        }

        public double NextDouble01() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);
    }

    public static class VariationSeed
    {
        /// <summary>FNV-1a по UTF-16 символам. Стабилен между платформами.</summary>
        public static ulong HashString(string value)
        {
            ulong hash = 1469598103934665603UL;
            if (string.IsNullOrEmpty(value)) return hash;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 1099511628211UL;
            }
            return hash;
        }

        /// <summary>
        /// Смешать внешний seed с profileId и солью профиля. profileId/соль входят в seed,
        /// поэтому одинаковый seed на разных профилях даёт разные роллы, а один профиль —
        /// воспроизводим.
        /// </summary>
        public static ulong Derive(long seed, string profileId, int salt)
        {
            ulong hash = HashString(profileId);
            hash ^= unchecked((ulong)seed);
            hash += unchecked((ulong)(uint)salt) * 0x9E3779B97F4A7C15UL;
            hash ^= hash >> 33;
            hash *= 0xFF51AFD7ED558CCDUL;
            hash ^= hash >> 33;
            return hash;
        }

        /// <summary>Новый случайный seed для редактора/лута. НЕ детерминирован.</summary>
        public static long NewRandomSeed()
        {
            long ticks = DateTime.UtcNow.Ticks;
            long mixed = ticks ^ ((long)Environment.TickCount << 32) ^ Guid.NewGuid().GetHashCode();
            return mixed == 0L ? 1L : mixed;
        }
    }
}
