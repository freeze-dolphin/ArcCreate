using ArcCreate.ChartFormat;
using UnityEngine;

namespace ArcCreate.Gameplay.Data
{
    public class CameraEvent : ArcEvent
    {
        public ExpressionValue<float> MoveX { get; set; } = 0;
        public ExpressionValue<float> MoveY { get; set; } = 0;
        public ExpressionValue<float> MoveZ { get; set; } = 0;
        public ExpressionValue<float> RotateX { get; set; } = 0;
        public ExpressionValue<float> RotateY { get; set; } = 0;
        public ExpressionValue<float> RotateZ { get; set; } = 0;

        public Vector3 Move
        {
            get => new(MoveX.Value, MoveY.Value, MoveZ.Value);
            set
            {
                MoveX = value.x;
                MoveY = value.y;
                MoveZ = value.z;
            }
        }

        public Vector3 Rotate
        {
            get => new(RotateX.Value, RotateY.Value, RotateZ.Value);
            set
            {
                RotateX = value.x;
                RotateY = value.y;
                RotateZ = value.z;
            }
        }

        public CameraType CameraType { get; set; }

        public ExpressionValue<int> Duration { get; set; } = 0;

        public bool IsReset => CameraType == CameraType.Reset;

        public override ArcEvent Clone()
        {
            return new CameraEvent
            {
                Timing = Timing,
                MoveX = MoveX,
                MoveY = MoveY,
                MoveZ = MoveZ,
                RotateX = RotateX,
                RotateY = RotateY,
                RotateZ = RotateZ,
                Duration = Duration,
                CameraType = CameraType,
                TimingGroup = TimingGroup,
            };
        }

        public override void Assign(ArcEvent newValues)
        {
            base.Assign(newValues);
            CameraEvent n = (newValues as CameraEvent)!;
            MoveX = n.MoveX;
            MoveY = n.MoveY;
            MoveZ = n.MoveZ;
            RotateX = n.RotateX;
            RotateY = n.RotateY;
            RotateZ = n.RotateZ;
            CameraType = n.CameraType;
            Duration = n.Duration;
            TimingGroup = n.TimingGroup;
        }

        public int CompareTo(CameraEvent other)
        {
            if (Timing == other.Timing)
            {
                return Duration.CompareTo(other.Duration);
            }

            return Timing.CompareTo(other.Timing);
        }

        public float PercentAt(int timing)
        {
            if (timing > Timing + Duration)
            {
                return 1;
            }
            else if (timing < Timing)
            {
                return 0;
            }

            float p = Mathf.Clamp((float)(timing - Timing) / Duration, 0, 1);
            switch (CameraType)
            {
                case CameraType.Qi:
                    return ArcFormula.Qi(p);
                case CameraType.Qo:
                    return ArcFormula.Qo(p);
                case CameraType.S:
                    return ArcFormula.B(0, 1, p);
                default:
                    return p;
            }
        }
    }
}