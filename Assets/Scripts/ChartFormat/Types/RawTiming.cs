namespace ArcCreate.ChartFormat
{
    public class RawTiming : RawEvent
    {
        public ExpressionValue<float> Bpm { get; set; }

        public ExpressionValue<float> Divisor { get; set; }
    }
}