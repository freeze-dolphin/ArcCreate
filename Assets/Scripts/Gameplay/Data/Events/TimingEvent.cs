using ArcCreate.ChartFormat;

namespace ArcCreate.Gameplay.Data
{
    public class TimingEvent : ArcEvent
    {
        public double FloorPosition { get; set; }

        public ExpressionValue<float> Bpm { get; set; } = 0;

        public ExpressionValue<float> Divisor { get; set; } = 0;

        public override ArcEvent Clone()
        {
            return new TimingEvent()
            {
                Timing = Timing,
                Bpm = Bpm,
                Divisor = Divisor,
                TimingGroup = TimingGroup,
            };
        }

        public override void Assign(ArcEvent newValues)
        {
            base.Assign(newValues);
            TimingEvent n = newValues as TimingEvent;
            Bpm = n.Bpm;
            Divisor = n.Divisor;
        }
    }
}
