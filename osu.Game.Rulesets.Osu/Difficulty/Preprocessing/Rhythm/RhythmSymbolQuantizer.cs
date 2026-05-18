// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing.Rhythm
{
    public static class RhythmSymbolQuantizer
    {
        public const int RATIO_BIN_COUNT = 21;

        // Covers most beat snap divisor ratios
        // Certain ratios were omitted due to being exceedingly close in log-space and prone to being confused for BPM timing point noise
        private static readonly double[] target_ratios = new[]
        {
            0.0625, 0.08333, 0.125, 0.16667, 0.250, 0.33333, 0.400, 0.500, 0.66667, 0.750, // Bins 0 to 9
            1.000, // Bin 10
            1.33333, 1.500, 2.000, 2.500, 3.000, 4.000, 6.000, 8.000, 12.000, 16.000 // Bins 11 to 20
        };

        // A list of precomputed boundary sizes between the target ratios in log-space
        private static readonly double[] log_boundaries = new double[RATIO_BIN_COUNT - 1];

        static RhythmSymbolQuantizer()
        {
            // Precompute the windows exactly once when the class assembly loads
            for (int i = 0; i < log_boundaries.Length; i++)
            {
                double logCurrent = Math.Log(target_ratios[i]);
                double logNext = Math.Log(target_ratios[i + 1]);

                // The decision threshold is the exact geometric midpoint in log space
                log_boundaries[i] = (logCurrent + logNext) / 2.0;
            }
        }

        // Center bin index (ratios close to 1.0)
        private const int center_bin = 10;

        public static int QuantizeRatio(double currDelta, double prevDelta, double epsilon)
        {
            // Deltas within the OD hit window are indistinguishable, snap to center
            if (Math.Abs(currDelta - prevDelta) < epsilon)
                return center_bin;

            double ratio = currDelta / prevDelta;
            double logRatio = Math.Log(ratio);

            // Linear scan across the precomputed log boundaries
            for (int i = 0; i < log_boundaries.Length; i++)
            {
                if (logRatio < log_boundaries[i])
                    return i;
            }

            // Fallback for ratios greater than the upper threshold of 16:1
            return RATIO_BIN_COUNT - 1;
        }
    }
}
