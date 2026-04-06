using System.Collections.Generic;
using System.Globalization;
using ArcCreate.Utility.Parser;
using UnityEngine;

namespace ArcCreate.ChartFormat
{
    public class RawArc : RawEvent
    {
        public ExpressionValue<int> EndTiming { get; set; } = 0;

        public ExpressionValue<float> XStart { get; set; } = 0;

        public ExpressionValue<float> XEnd { get; set; } = 0;

        public string LineType { get; set; }

        public ExpressionValue<float> YStart { get; set; } = 0;

        public ExpressionValue<float> YEnd { get; set; } = 0;

        public ExpressionValue<int> Color { get; set; } = 0;

        public bool IsTrace { get; set; }

        public string Sfx { get; set; }

        public List<RawArcTap> ArcTaps { get; set; }

        #region Property

        public ExpressionValue<float> ArcResolution { get; set; } = 1;

        public Color? TraceColor { get; set; } = null;

        #endregion
    }
}