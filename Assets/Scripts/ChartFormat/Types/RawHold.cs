namespace ArcCreate.ChartFormat
{
    public class RawHold : RawEvent
    {
        public ExpressionValue<int> EndTiming { get; set; }

        public ExpressionValue<float> Lane { get; set; }
    }
}