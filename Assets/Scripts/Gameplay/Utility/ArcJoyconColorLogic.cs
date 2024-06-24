using System;
using System.Collections.Generic;

namespace ArcCreate.Gameplay
{
    public class ArcJoyconColorLogic
    {
        private static readonly List<ArcJoyconColorLogic> Instances = new();
        private static int frameTiming;
        private int lockUntil = int.MinValue;
        public float CurrentRedArcValue => IsInputLocked ? 1 : 0;

        public bool IsInputLocked
            => frameTiming <= lockUntil;

        public static int MaxColor => Instances.Count - 1;
        public int Color { get; }

        private ArcJoyconColorLogic(int color)
        {
            Color = color;
        }

        public static void NewFrame(int timing)
        {
            frameTiming = timing;
        }

        public static ArcJoyconColorLogic Get(int color)
        {
            if (color < 0)
            {
                throw new Exception();
            }

            while (color >= Instances.Count)
            {
                Instances.Add(new ArcJoyconColorLogic(Instances.Count));
            }

            return Instances[color];
        }

        public static void ResetAll()
        {
            for (var color = 0; color < Instances.Count; color++)
            {
                Services.Skin.ApplyRedArcValue(color, 0);
            }

            Instances.Clear();
        }

        public static void ApplyRedValue()
        {
            for (var color = 0; color < Instances.Count; color++)
            {
                Services.Skin.ApplyRedArcValue(color, Instances[color].CurrentRedArcValue);
            }
        }

        public static void UpdteRedValue()
        {
            for (var color = 0; color < Instances.Count; color++)
            {
                var logic = Instances[color];
                if (!logic.IsInputLocked) logic.UnlockInput();
            }
        }

        public void ExistsArcWithinRange(bool exists)
        {
            if (!exists)
            {
                UnlockInput();
            }
        }

        public void LockInput(float arcJudgeInterval)
        {
            var lockDuration = ArcFormula.CalculateArcLockDuration(arcJudgeInterval);
            lockUntil = frameTiming + lockDuration;
        }

        private void UnlockInput()
        {
            lockUntil = int.MinValue;
        }
    }
}