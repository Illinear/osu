// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing.Rhythm
{
    public static class ClusterDifficultyPreprocessor
    {
        private const int ctw_max_depth = 3;

        public static void ProcessAndAssign(List<RhythmClusterData>? clusters)
        {
            if (clusters == null || clusters.Count == 0)
                return;

            var parityCtw = new ContextTreeWeighting(ctw_max_depth, 2, clusters.Count);
            var gapCtw = new ContextTreeWeighting(ctw_max_depth, RhythmSymbolQuantizer.RATIO_BIN_COUNT, clusters.Count);
            var internalCtw = new ContextTreeWeighting(ctw_max_depth, RhythmSymbolQuantizer.RATIO_BIN_COUNT, clusters.Count);

            foreach (RhythmClusterData c in clusters)
            {
                parityCtw.ConstructTreeNode(c.ParitySymbol);
                gapCtw.ConstructTreeNode(c.GapSymbol);
                internalCtw.ConstructTreeNode(c.InternalSymbol);
            }

            parityCtw.FinalizeTreeProbs();
            gapCtw.FinalizeTreeProbs();
            internalCtw.FinalizeTreeProbs();

            foreach (RhythmClusterData c in clusters)
            {
                var parityResult = parityCtw.EvaluateTreeNode(c.ParitySymbol);
                var gapResult = gapCtw.EvaluateTreeNode(c.GapSymbol);
                var internalResult = internalCtw.EvaluateTreeNode(c.InternalSymbol);

                c.ParitySurprisal = parityResult.Surprisal;
                c.GapSurprisal = gapResult.Surprisal;
                c.InternalSurprisal = internalResult.Surprisal;
            }
        }
    }
}
