using System.Collections.Generic;
using System.Globalization;
using ArcCreate.Utility.Parser;
using UnityEngine;

namespace ArcCreate.ChartFormat
{
    public class RawArc : RawEvent
    {
        public int EndTiming { get; set; }

        public float XStart { get; set; }

        public float XEnd { get; set; }

        public string LineType { get; set; }

        public float YStart { get; set; }

        public float YEnd { get; set; }

        public int Color { get; set; }

        public bool IsTrace { get; set; }

        public string Sfx { get; set; }

        public List<RawArcTap> ArcTaps { get; set; }

        #region Property

        public const string PropertyArcResolutionKey = "resolution";

        public float ArcResolution
        {
            get => !Evaluator.TryInt(Properties.GetValueOrDefault(PropertyArcResolutionKey, null), out int resolution)
                ? 1
                : resolution;
            set => Properties[PropertyArcResolutionKey] = value.ToString(CultureInfo.InvariantCulture);
        }

        public const string PropertyStainedColorKey = "stained";

        public Color? StainedColor
        {
            get
            {
                var colorRaw = Properties.GetValueOrDefault(PropertyStainedColorKey, null);
                if (colorRaw == null) return null;

                return ColorUtility.TryParseHtmlString("#" + colorRaw.TrimStart('#'), out var color)
                    ? color
                    : null;
            }
            set => Properties[PropertyStainedColorKey] = value.HasValue
                ? "#" + ColorUtility.ToHtmlStringRGB(value.Value)
                : null;
        }

        public bool TryGetStainedColor(out Color color)
        {
            var c = StainedColor;
            color = c ?? UnityEngine.Color.black;

            return c.HasValue;
        }

        #endregion
    }
}