// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing.Rhythm
{
    public class ContextTreeWeighting
    {
        private readonly int maxDepth;
        private readonly int alphabetSize;

        // private readonly double[] prior;
        // private readonly double priorBaseEntropy;
        private readonly int[] contextBuffer;
        private int bufferCount;

        private readonly int[] countsPool;
        private readonly int[] childIndicesPool;
        private readonly double[] logProbKtPool;
        private readonly double[] logProbWeightedPool;
        private readonly int[] totalCountPool;

        private int nodeCount;
        private const int root_index = 0;

        private readonly double[] logGammaNumerator;
        private readonly double[] logGammaDenominator;

        private static readonly double[] jacobian_table = new double[512];
        private const double jacobian_max_diff = 5.0; // Beyond a difference of 5.0, the correction is mathematically negligible
        private const double jacobian_scale = 512.0 / jacobian_max_diff;

        private const double log_two = 0.6931471805599453; // Math.Log(2)

        static ContextTreeWeighting()
        {
            // Populate the Jacobian correction table factor: ln(1 + e^-x)
            for (int i = 0; i < 512; i++)
            {
                double x = i / jacobian_scale;
                jacobian_table[i] = Math.Log(1.0 + Math.Exp(-x));
            }
        }

        public ContextTreeWeighting(int maxDepth, int alphabetSize, int clusterCount)
        {
            this.maxDepth = maxDepth;
            this.alphabetSize = alphabetSize;
            // this.prior = prior;

            // priorBaseEntropy = 0;
            //
            // foreach (double q in prior)
            // {
            //     if (q > 0)
            //         priorBaseEntropy -= q * Math.Log(q, 2);
            // }

            contextBuffer = new int[maxDepth];

            int cacheSizeNeeded = clusterCount + 1;
            logGammaNumerator = new double[cacheSizeNeeded + 1];
            logGammaDenominator = new double[cacheSizeNeeded + 1];

            for (int i = 0; i <= cacheSizeNeeded; i++)
            {
                logGammaNumerator[i] = Math.Log(i + 0.5);
                logGammaDenominator[i] = Math.Log(i + alphabetSize / 2.0);
            }

            // Calculate exact total required memory capacity based on dynamic parameters
            int maxPossibleNodes = clusterCount * maxDepth + 1;

            // Every node takes up exactly alphabetSize elements sequentially
            countsPool = new int[maxPossibleNodes * alphabetSize];
            childIndicesPool = new int[maxPossibleNodes * alphabetSize];

            // Metadata node arrays
            logProbKtPool = new double[maxPossibleNodes];
            logProbWeightedPool = new double[maxPossibleNodes];
            totalCountPool = new int[maxPossibleNodes];

            resetPool();
        }

        private void resetPool()
        {
            nodeCount = 1; // Root occupies index 0
            Array.Clear(countsPool, 0, countsPool.Length);
            Array.Fill(childIndicesPool, -1); // Initialize all branching lanes as leaf nodes
            Array.Clear(logProbKtPool, 0, logProbKtPool.Length);
            Array.Clear(logProbWeightedPool, 0, logProbWeightedPool.Length);
            Array.Clear(totalCountPool, 0, totalCountPool.Length);
        }

        public struct EvaluationResult
        {
            public double Surprisal;
            public double CrossEntropy;
        }

        public EvaluationResult EvaluateTreeNode(int symbol)
        {
            int maxSearchDepth = Math.Min(bufferCount, maxDepth);
            Span<int> pathIndices = stackalloc int[maxSearchDepth + 1];
            pathIndices[0] = root_index;

            int actualDepth = 0;

            for (int d = 0; d < maxSearchDepth; d++)
            {
                int contextSymbol = contextBuffer[(bufferCount - 1 - d) % maxDepth];
                int childIdx = childIndicesPool[pathIndices[d] * alphabetSize + contextSymbol];

                if (childIdx == -1)
                    break;

                pathIndices[d + 1] = childIdx;
                actualDepth++;
            }

            double logProbMixed = 0;

            for (int d = actualDepth; d >= 0; d--)
            {
                int currentIdx = pathIndices[d];
                int countOffset = currentIdx * alphabetSize + symbol;

                int symbolCount = countsPool[countOffset];
                int totalCount = totalCountPool[currentIdx];

                double logProbKt = logGammaNumerator[symbolCount] - logGammaDenominator[totalCount];

                if (d == actualDepth)
                {
                    logProbMixed = logProbKt;
                }
                else
                {
                    logProbMixed = -log_two + LogSumExp(logProbKt, logProbMixed);
                }
            }

            double surprisal = -logProbMixed / log_two;

            // double crossEntropy = 0;
            // int deepNodeIdx = pathIndices[actualDepth];
            // int deepStrideOffset = deepNodeIdx * alphabetSize;
            //
            // for (int i = 0; i < alphabetSize; i++)
            // {
            //     double p = (countsPool[deepStrideOffset + i] + 0.5) / (totalCountPool[deepNodeIdx] + alphabetSize / 2.0);
            //     double q = prior[i];
            //
            //     crossEntropy += p * -Math.Log(q, 2);
            // }
            //
            // double finalCrossEntropy = Math.Max(0, crossEntropy - priorBaseEntropy);

            // Update context buffer for the next note evaluation step
            contextBuffer[bufferCount % maxDepth] = symbol;
            bufferCount++;

            return new EvaluationResult
            {
                Surprisal = surprisal,
                CrossEntropy = 0.0
            };
        }

        public void ConstructTreeNode(int symbol)
        {
            int depth = Math.Min(bufferCount, maxDepth);
            Span<int> pathIndices = stackalloc int[depth + 1];
            pathIndices[0] = root_index;

            for (int d = 0; d < depth; d++)
            {
                int contextSymbol = contextBuffer[(bufferCount - 1 - d) % maxDepth];
                pathIndices[d + 1] = getOrCreateChild(pathIndices[d], contextSymbol);
            }

            for (int d = depth; d >= 0; d--)
            {
                int idx = pathIndices[d];
                countsPool[idx * alphabetSize + symbol]++;
                totalCountPool[idx]++;
            }

            contextBuffer[bufferCount % maxDepth] = symbol;
            bufferCount++;
        }

        private int getOrCreateChild(int parentIdx, int symbol)
        {
            int parentStrideOffset = parentIdx * alphabetSize;
            int childIdx = childIndicesPool[parentStrideOffset + symbol];

            if (childIdx == -1)
            {
                int maxNodesCap = countsPool.Length / alphabetSize;
                if (nodeCount >= maxNodesCap) return root_index;

                childIdx = nodeCount++;
                childIndicesPool[parentStrideOffset + symbol] = childIdx;
            }

            return childIdx;
        }

        public void FinalizeTreeProbs()
        {
            finalizeProbabilitiesRecursive(root_index, 0);

            bufferCount = 0;
            Array.Clear(contextBuffer, 0, contextBuffer.Length);
        }

        private void finalizeProbabilitiesRecursive(int nodeIdx, int currentDepth)
        {
            int strideOffset = nodeIdx * alphabetSize;

            for (int i = 0; i < alphabetSize; i++)
            {
                int childIdx = childIndicesPool[strideOffset + i];
                if (childIdx != -1)
                    finalizeProbabilitiesRecursive(childIdx, currentDepth + 1);
            }

            double accumulatedLogKtNumerator = 0;

            for (int i = 0; i < alphabetSize; i++)
            {
                int count = countsPool[strideOffset + i];
                for (int c = 0; c < count; c++) accumulatedLogKtNumerator += logGammaNumerator[c];
            }

            double accumulatedLogKtDenominator = 0;
            int totalCount = totalCountPool[nodeIdx];

            for (int t = 0; t < totalCount; t++)
            {
                accumulatedLogKtDenominator += logGammaDenominator[t];
            }

            logProbKtPool[nodeIdx] = accumulatedLogKtNumerator - accumulatedLogKtDenominator;

            if (currentDepth == maxDepth)
            {
                logProbWeightedPool[nodeIdx] = logProbKtPool[nodeIdx];
                return;
            }

            double logProbChildren = 0;

            for (int i = 0; i < alphabetSize; i++)
            {
                int childIdx = childIndicesPool[strideOffset + i];
                if (childIdx != -1)
                    logProbChildren += logProbWeightedPool[childIdx];
            }

            logProbWeightedPool[nodeIdx] = -log_two + LogSumExp(logProbKtPool[nodeIdx], logProbChildren);
        }

        // Approximation of a canonical log-sum-exp using the Jacobian logarithm with a precomputed lookup table for correction factors e^-|a-b|
        public static double LogSumExp(double a, double b)
        {
            if (double.IsNegativeInfinity(a)) return b;
            if (double.IsNegativeInfinity(b)) return a;

            double max = Math.Max(a, b);
            double diff = Math.Abs(a - b);

            if (diff >= jacobian_max_diff)
                return max;

            int index = (int)(diff * jacobian_scale);

            return max + jacobian_table[index];
        }
    }
}
