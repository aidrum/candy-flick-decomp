using System;
using UnityEngine;

namespace CandyFlick
{
    // Recovered constants: ScoreBehaviour (TypeDef 1867), PlayerBehaviour (1865), Round (1905).
    public static class GameRules
    {
        public const int CandiesPerRound = 10;
        public const float MinDistance = 3f, MaxDistance = 11.05f;
        public const int HorizontalRingBonus = 10, VerticalRingBonus = 15;
        public static int DistanceScore(float metres, bool super = false)
            => DistanceScoreUnits(metres * 100f, super);
        public static int DistanceScoreUnits(float distance, bool super = false)
        {
            // ScoreBehaviour.GetScore, VA 0x27530: clamp(min + max(0,d-300)*(max-min)/805), truncate.
            int min = super ? 10 : 5, max = super ? 100 : 50;
            return (int)Mathf.Clamp(min + Mathf.Max(0, (int)distance-300) * ((max-min)/805f), min, max);
        }
    }

    [Serializable]
    public sealed class GameSession
    {
        public int shots, total, bonus, hits, baseTotal, ringTotal;
        public int round => Mathf.Min(3, shots / 3 + 1);
        public int[] scores = new int[GameRules.CandiesPerRound];
        public int[] boosts = new int[GameRules.CandiesPerRound];
        public int CompleteScore => total + bonus;
        public bool IsOver => shots >= GameRules.CandiesPerRound;
        public bool IsSuperCandy => shots == GameRules.CandiesPerRound - 1;
        public int Remaining => GameRules.CandiesPerRound - shots;
        public int Record(bool hit, float distance, int ringBonus)
            => RecordUnits(hit, distance * 100f, ringBonus);
        public int RecordUnits(bool hit, float distance, int ringBonus)
        {
            if (IsOver) throw new InvalidOperationException("Round is already over.");
            int baseScore = hit ? GameRules.DistanceScoreUnits(distance,IsSuperCandy) : 0;
            int earnedRings = hit ? ringBonus : 0;
            int score = baseScore + earnedRings;
            baseTotal += baseScore;
            ringTotal += earnedRings;
            boosts[shots] = earnedRings;
            scores[shots++] = score;
            total += score;
            if (hit) hits++;
            return score;
        }
    }
}
