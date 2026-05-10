using System;
using DiceGame.Core.Models;
using DiceGame.Core.Rules;
using DiceGame.Core.Systems;

namespace DiceGame.Core.Networking
{
    // Deterministic 32-bit FNV-1a hash over the lockstep-relevant state of a MatchManager.
    //
    // What's hashed (in this exact order, byte-for-byte identical on every peer):
    //   1. turnIndex (int32 LE)
    //   2. cup.RollsLeft (int32 LE)
    //   3. for each die in cup.Dice (in array order): Value (int32 LE), IsHeld (1 byte: 0 or 1)
    //   4. for each player ordered by Player.Id ascending:
    //        for each ScoreCategory in Enum order:
    //          1 byte filled-flag (0 = empty, 1 = filled)
    //          int32 LE score (0 when empty so collisions across "empty" are stable)
    //        1 byte IsBonusClaimed (0 or 1)
    //
    // Anything NOT in this list is presentation-only and must NOT influence the hash:
    //   - dice scatter positions / rotations (Unity RNG-driven, presentation only)
    //   - which UI panel is visible
    //   - timestamps / wall-clock
    //
    // Why FNV-1a: tiny, allocation-free, deterministic across .NET runtimes (no salting),
    // and a 32-bit collision space is plenty for end-of-turn drift detection between two and
    // four peers. We are NOT using this hash for security.
    public static class LockstepHasher
    {
        private const uint FnvOffsetBasis = 2166136261;
        private const uint FnvPrime = 16777619;

        // Stable category iteration order. Enum.GetValues() returns CLR-defined order which is
        // stable for a given build, but we lock it down explicitly so adding a new category
        // doesn't silently change historic hash values for old categories.
        private static readonly ScoreCategory[] HashedCategories = new[]
        {
            ScoreCategory.Ones,
            ScoreCategory.Twos,
            ScoreCategory.Threes,
            ScoreCategory.Fours,
            ScoreCategory.Fives,
            ScoreCategory.Sixes,
            ScoreCategory.ThreeOfAKind,
            ScoreCategory.FourOfAKind,
            ScoreCategory.FullHouse,
            ScoreCategory.SmallStraight,
            ScoreCategory.LargeStraight,
            ScoreCategory.NicerDicer,
            ScoreCategory.Chance
        };

        public static int Compute(MatchManager match, int turnIndex)
        {
            if (match == null) return 0;

            uint h = FnvOffsetBasis;

            h = MixInt32(h, turnIndex);

            var cup = match.Cup;
            if (cup != null)
            {
                h = MixInt32(h, cup.RollsLeft);
                if (cup.Dice != null)
                {
                    for (int i = 0; i < cup.Dice.Count; i++)
                    {
                        var die = cup.Dice[i];
                        if (die == null) continue;
                        h = MixInt32(h, die.Value);
                        h = MixByte(h, die.IsHeld ? (byte)1 : (byte)0);
                    }
                }
            }

            if (match.Players != null)
            {
                // Sort by Id ascending. We assume MatchManager.Players is already in id order
                // (it is, by construction in GameController), but defensively iterate by id.
                int playerCount = match.Players.Count;
                for (int targetId = 0; targetId < playerCount; targetId++)
                {
                    Player p = null;
                    for (int j = 0; j < playerCount; j++)
                    {
                        if (match.Players[j] != null && match.Players[j].Id == targetId)
                        {
                            p = match.Players[j];
                            break;
                        }
                    }
                    if (p == null) continue;

                    var sc = p.ScoreCard;
                    if (sc == null) continue;

                    for (int c = 0; c < HashedCategories.Length; c++)
                    {
                        var cat = HashedCategories[c];
                        bool filled = sc.IsCategoryFilled(cat);
                        h = MixByte(h, filled ? (byte)1 : (byte)0);
                        int score = filled ? (sc.GetScore(cat) ?? 0) : 0;
                        h = MixInt32(h, score);
                    }
                    h = MixByte(h, sc.IsBonusClaimed ? (byte)1 : (byte)0);
                }
            }

            // Reinterpret as signed int32 so the wire format (which uses int32 LE) round-trips
            // cleanly without modular arithmetic surprises on different runtimes.
            return unchecked((int)h);
        }

        private static uint MixByte(uint h, byte b)
        {
            h ^= b;
            h *= FnvPrime;
            return h;
        }

        private static uint MixInt32(uint h, int v)
        {
            uint u = unchecked((uint)v);
            h = MixByte(h, (byte)(u & 0xFF));
            h = MixByte(h, (byte)((u >> 8) & 0xFF));
            h = MixByte(h, (byte)((u >> 16) & 0xFF));
            h = MixByte(h, (byte)((u >> 24) & 0xFF));
            return h;
        }
    }
}
