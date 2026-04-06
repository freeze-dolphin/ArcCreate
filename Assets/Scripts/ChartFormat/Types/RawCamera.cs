using UnityEngine;

namespace ArcCreate.ChartFormat
{
    public class RawCamera : RawEvent
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

        public string CameraType { get; set; }

        public ExpressionValue<int> Duration { get; set; } = 0;
    }
}