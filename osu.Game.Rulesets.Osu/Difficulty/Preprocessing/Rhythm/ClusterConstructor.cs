// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing.Rhythm
{
    public static class ClusterConstructor
    {
        private const double epsilon_factor = 0.3;

        /// <summary>
        /// Scans a list of <see cref="DifficultyHitObject"/> and arranges the hit objects into clusters.
        /// A cluster is defined as a series of equidistant notes - each cluster has an associated <see cref="RhythmClusterData"/> which tracks cluster timing and identity.
        /// </summary>
        public static List<RhythmClusterData> CreateClusters(List<DifficultyHitObject> objects)
        {
            var activeObjects = new List<OsuDifficultyHitObject>();

            foreach (var obj in objects)
            {
                if (obj is OsuDifficultyHitObject osuObj && osuObj.BaseObject is not Objects.Spinner)
                    activeObjects.Add(osuObj);
            }

            if (activeObjects.Count < 2) return new List<RhythmClusterData>();

            var clusterSequence = new List<RhythmClusterData>();

            int i = 0;
            double prevGapDelta = activeObjects[1].StartTime - activeObjects[0].StartTime;
            double prevInternalDelta = activeObjects[1].StartTime - activeObjects[0].StartTime;
            int clusterIndex = 0;

            while (i < activeObjects.Count - 1)
            {
                int start = i;
                bool endsWithPivot = false;
                double d1 = activeObjects[i + 1].StartTime - activeObjects[i].StartTime; // Current delta
                double d2 = i + 2 < activeObjects.Count ? activeObjects[i + 2].StartTime - activeObjects[i + 1].StartTime : double.PositiveInfinity; // Previous delta
                double epsilon = activeObjects[i].HitWindow(HitResult.Great) * epsilon_factor;

                // If we are speeding up, check the next note
                if (d1 > d2 + epsilon)
                    i++;

                // If we are at a stable rhythm, include all future notes that match this delta within tolerance
                else if (Math.Abs(d1 - d2) < epsilon)
                {
                    while (i + 1 < activeObjects.Count &&
                           Math.Abs(activeObjects[i + 1].StartTime - activeObjects[i].StartTime - d1) < epsilon)
                        i++;
                }

                // If we are slowing down, check the next note
                else
                    i++;

                int end = i;

                // Extract the metrics needed to define the RhythmClusterData
                int size = end - start + 1;
                double startTime = activeObjects[start].StartTime;
                double endTime = activeObjects[end].StartTime;

                // Gap delta is defined as the silence leading out of the last cluster into the start of this one
                double gapDelta = start > 0 ? startTime - activeObjects[start - 1].StartTime : d1;

                // Internal delta is defined as the inner pulse of the cluster: if a "singlet" / size-1 cluster ever arises, it is simply assigned the gap delta
                double internalDelta = size > 1 ? (endTime - startTime) / (size - 1) : gapDelta;

                // Quantize the gap and internal deltas against those of the previous cluster
                int gapSymbol = RhythmSymbolQuantizer.QuantizeRatio(gapDelta, prevGapDelta, epsilon);
                int internalSymbol = RhythmSymbolQuantizer.QuantizeRatio(internalDelta, prevInternalDelta, epsilon);

                // Parity is defined as whether a cluster has an even or odd object count: tracks finger alternation symmetry
                int paritySymbol = size % 2;

                // Starting object index
                int startingObjectIndex = activeObjects[start].Index;

                // Lookahead check to handle cases where a note may belong to two clusters: the start of one and the end of another
                if (i < activeObjects.Count - 1)
                {
                    double dAfter = activeObjects[i + 1].StartTime - activeObjects[i].StartTime;
                    double dAfterNext = i + 2 < activeObjects.Count ? activeObjects[i + 2].StartTime - activeObjects[i + 1].StartTime : 0;

                    bool fasterOrEqual = dAfter <= d1 + epsilon;

                    bool slowerButStable = dAfter > d1 + epsilon &&
                                           Math.Abs(dAfterNext - dAfter) < epsilon;

                    if (fasterOrEqual || slowerButStable)
                    {
                        // Pivot index is preserved to capture the continuous structural change
                        endsWithPivot = true;
                    }
                }

                var data = new RhythmClusterData(
                    clusterIndex++,
                    startingObjectIndex,
                    endsWithPivot,
                    startTime,
                    endTime,
                    gapDelta,
                    gapSymbol,
                    internalDelta,
                    internalSymbol,
                    size,
                    paritySymbol
                );

                clusterSequence.Add(data);

                // Assign data back to the hit objects for the evaluators to consume
                for (int j = start; j <= end; j++)
                {
                    if (activeObjects[j].PrimaryRhythmCluster == null)
                        activeObjects[j].PrimaryRhythmCluster = data;
                    else
                        activeObjects[j].OverlapRhythmCluster = data; // If it is a pivot note that semantically belongs to two clusters
                }

                prevInternalDelta = internalDelta;
                prevGapDelta = gapDelta;

                if (endsWithPivot)
                {
                    // Do not advance, the next iteration will reuse this note as the start of the next cluster
                }
                else
                {
                    // Terminal tail, advance past the boundary
                    i++;
                }
            }

            return clusterSequence;
        }
    }
}
