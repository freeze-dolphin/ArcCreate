using System.Collections.Generic;
using UnityEngine;

namespace ArcCreate.Gameplay.Render
{
    public class ArcDrawCallComparer : IComparer<ArcDrawCall>
    {
        private static int ChainCompare(params int[] compares)
        {
            foreach (int cmp in compares)
            {
                if (cmp != 0) return cmp;
            }

            return compares[^1];
        }

        private static int ByColorId(ArcDrawCall a, ArcDrawCall b) =>
            a.ColorId.CompareTo(b.ColorId);

        private static int ByDepth(ArcDrawCall a, ArcDrawCall b) =>
            a.Depth.CompareTo(b.Depth);

        private static int ByYPos(ArcDrawCall a, ArcDrawCall b, bool byEnd = false) =>
            byEnd
                ? a.ArcEndPos.y.CompareTo(b.ArcEndPos.y)
                : a.ArcStartPos.y.CompareTo(b.ArcStartPos.y);

        private static int ByXPosAbs(ArcDrawCall a, ArcDrawCall b, bool byEnd = false) =>
            byEnd
                ? Mathf.Abs(a.ArcEndPos.x - .5f).CompareTo(Mathf.Abs(b.ArcEndPos.x - .5f))
                : Mathf.Abs(a.ArcStartPos.x - .5f).CompareTo(Mathf.Abs(b.ArcStartPos.x - .5f));

        private static int ByArcTiming(ArcDrawCall a, ArcDrawCall b, bool byEnd = false) =>
            byEnd
                ? a.TimingStartEnd.y.CompareTo(b.TimingStartEnd.y)
                : a.TimingStartEnd.x.CompareTo(b.TimingStartEnd.x);

        public int Compare(ArcDrawCall a, ArcDrawCall b) =>
            ChainCompare(
                ByYPos(a, b),
                ByYPos(a, b, true),
                ByColorId(a, b),
                -ByXPosAbs(a,b),
                -ByXPosAbs(a,b, true),
                -ByArcTiming(a, b),
                -ByArcTiming(a, b, true)
            );
    }
}