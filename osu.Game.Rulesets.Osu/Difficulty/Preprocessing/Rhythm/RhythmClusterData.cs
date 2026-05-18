// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Preprocessing.Rhythm
{
    /// <summary>
    /// A container for the properties of a rhythm cluster, defined as a series of equidistant <see cref="DifficultyHitObject"/>.
    /// Contains timing, identity and statistical difficulty information.
    /// </summary>
    public class RhythmClusterData
    {
        public readonly int Index;
        public readonly int StartingObjectIndex;
        public readonly bool EndsWithPivot;
        public readonly double StartTime;
        public readonly double EndTime;

        public readonly double GapDelta;
        public readonly int GapSymbol;

        public readonly double InternalDelta;
        public readonly int InternalSymbol;

        public readonly int Size;
        public readonly int ParitySymbol;

        public double GapSurprisal { get; set; }
        public double InternalSurprisal { get; set; }
        public double ParitySurprisal { get; set; }

        public RhythmClusterData(int index, int startingObjectIndex, bool endsWithPivot, double start, double end,
                                 double gapDelta, int gapSymbol,
                                 double internalDelta, int internalSymbol,
                                 int size, int paritySymbol)
        {
            Index = index;
            StartingObjectIndex = startingObjectIndex;
            EndsWithPivot = endsWithPivot;
            StartTime = start;
            EndTime = end;

            GapDelta = gapDelta;
            GapSymbol = gapSymbol;

            InternalDelta = internalDelta;
            InternalSymbol = internalSymbol;

            Size = size;
            ParitySymbol = paritySymbol;
        }
    }
}
