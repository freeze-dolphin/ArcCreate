using System.Collections.Generic;
using System.Globalization;
using ArcCreate.Utility.Parser;
using UnityEngine;

namespace ArcCreate.ChartFormat
{
    public class RawArc : RawEvent
    {
        public ExpressionValue<int> EndTiming { get; set; }

        public ExpressionValue<float> XStart { get; set; }

        public ExpressionValue<float> XEnd { get; set; }

        public string LineType { get; set; }

        public ExpressionValue<float> YStart { get; set; }

        public ExpressionValue<float> YEnd { get; set; }

        public ExpressionValue<int> Color { get; set; }

        public bool IsTrace { get; set; }

        public string Sfx { get; set; }

        public List<RawArcTap> ArcTaps { get; set; }

        #region Property
        
        /*
         * PROPERTY KEYS MUST BE IN LOWERCASE
         */

        public const string PropertyArcResolutionKey = "resolution";

        public float ArcResolution
        {
            get => !Evaluator.TryInt(Properties.GetValueOrDefault(PropertyArcResolutionKey, null), out int resolution)
                ? 1
                : resolution;
            set => Properties[PropertyArcResolutionKey] = value.ToString(CultureInfo.InvariantCulture);
        }

        public const string PropertyTraceColorKey = "tracecolor";

        public Color? TraceColor
        {
            get
            {
                var colorRaw = Properties.GetValueOrDefault(PropertyTraceColorKey, null);
                if (colorRaw == null) return null;

                return ColorUtility.TryParseHtmlString("#" + colorRaw.TrimStart('#'), out var color)
                    ? color
                    : null;
            }
            set => Properties[PropertyTraceColorKey] = value.HasValue
                ? "#" + ColorUtility.ToHtmlStringRGB(value.Value)
                : null;
        }

        public bool TryGetTraceColor(out Color color)
        {
            var c = TraceColor;
            color = c ?? UnityEngine.Color.black;

            return c.HasValue;
        }

        #endregion
    }
}