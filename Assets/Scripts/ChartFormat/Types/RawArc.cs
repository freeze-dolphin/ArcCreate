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

        public const string ArcResolutionKey = "resolution";

        public float ArcResolution
        {
            get => !Evaluator.TryInt(Properties.GetValueOrDefault(ArcResolutionKey, null), out int resolution)
                ? 1
                : resolution;
            set => Properties[ArcResolutionKey] = value.ToString(CultureInfo.InvariantCulture);
        }

        public const string StainedColorKey = "stained";
        public static Color32 DesignantColor = new Color32(240, 41, 97, byte.MaxValue);

        public Color? StainedColor
        {
            get => !ColorUtility.TryParseHtmlString(Properties.GetValueOrDefault(StainedColorKey, null), out var color)
                ? null
                : color;
            set => Properties[StainedColorKey] = value.HasValue ? ColorUtility.ToHtmlStringRGB(value.Value) : null;
        }

        public bool TryGetStainedColor(out Color color)
        {
            var c = StainedColor;
            color = c.HasValue ? c.Value : UnityEngine.Color.black;

            return c.HasValue;
        }

        #endregion
    }
}