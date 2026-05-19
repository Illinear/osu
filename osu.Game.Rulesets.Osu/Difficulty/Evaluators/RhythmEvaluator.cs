// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing.Rhythm;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    /// <summary>
    /// Compute the local CTW surprise utilizing time-scaling factors and physical double-tap suppression.
    /// </summary>
    public static class RhythmEvaluator
    {
        private const double overall_multiplier = 1.0;
        private const double min_bpm_threshold = 210.0;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuObj = (OsuDifficultyHitObject)current;
            double totalComplexity = 0;

            if (osuObj.PrimaryRhythmCluster != null)
            {
                totalComplexity += processClusterSurprisal(osuObj, osuObj.PrimaryRhythmCluster);
            }

            if (osuObj.OverlapRhythmCluster != null)
            {
                totalComplexity += processClusterSurprisal(osuObj, osuObj.OverlapRhythmCluster);
                totalComplexity *= 0.5;
            }

            double timeScale = 1000.0 / Math.Max(current.DeltaTime, 1.0);

            double strainThreshold = DifficultyCalculationUtils.BPMToMilliseconds(min_bpm_threshold);
            double lowEndSuppression = Math.Pow(Math.Min(1.0, strainThreshold / current.DeltaTime), 2.0);

            var osuObjNext = (OsuDifficultyHitObject)osuObj.Next(0);
            double doubletapness = osuObjNext != null ? 1.0 - osuObj.GetDoubletapness(osuObjNext) : 1.0;

            return overall_multiplier * totalComplexity * timeScale * lowEndSuppression * doubletapness;
        }

        private static double processClusterSurprisal(OsuDifficultyHitObject osuObj, RhythmClusterData cluster)
        {
            // Check note position relative to cluster boundaries
            bool isFirstNote = Math.Abs(osuObj.StartTime - cluster.StartTime) < 1e-7;
            bool isLastNote = Math.Abs(osuObj.StartTime - cluster.EndTime) < 1e-7;

            // Inter-cluster (gap): entry shock of the transition
            double gapSurprisal = isFirstNote ? cluster.GapSurprisal : 0;

            // Intra-cluster (internal): sustained difficulty of internal rhythm spread evenly through the group
            double internalSurprisal = cluster.InternalSurprisal / cluster.Size;

            // Parity (resolution): exit shock of the transition
            double paritySurprisal = isLastNote ? cluster.ParitySurprisal : 0;

            return gapSurprisal + internalSurprisal + paritySurprisal;
        }
    }
}
