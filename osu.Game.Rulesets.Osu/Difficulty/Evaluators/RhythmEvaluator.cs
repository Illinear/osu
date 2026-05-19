// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class RhythmEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuObj = (OsuDifficultyHitObject)current;
            double totalSurprisal = 0;

            if (osuObj.PrimaryRhythmCluster != null)
            {
                var primary = osuObj.PrimaryRhythmCluster;
                totalSurprisal += primary.GapSurprisal + primary.InternalSurprisal + primary.ParitySurprisal;
            }

            if (osuObj.OverlapRhythmCluster != null)
            {
                var overlap = osuObj.OverlapRhythmCluster;
                totalSurprisal += overlap.GapSurprisal + overlap.InternalSurprisal + overlap.ParitySurprisal;
                totalSurprisal *= 0.5; // Average of primary and overlap cluster surprisal
            }

            return totalSurprisal;
        }
    }
}
