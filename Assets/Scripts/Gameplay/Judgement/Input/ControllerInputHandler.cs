using System;
using System.Collections.Generic;
using ArcCreate.Utility;
using UnityEngine;

namespace ArcCreate.Gameplay.Judgement.Input
{
    public class ControllerInputHandler : IInputHandler
    {
        private static void DebugKeyPressed()
        {
            foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
            {
                if (UnityEngine.Input.GetKeyDown(keyCode))
                {
                    Debug.Log("Pressed: " + keyCode);
                }
            }
        }

        public record JoyconArcInputState
        {
            public record JoyconSingleArcInputState
            {
                public float Horizontal { get; set; }
                public float Vertical { get; set; }

                public JoyconSingleArcInputState(float hori, float vert)
                {
                    Horizontal = hori;
                    Vertical = vert;
                }
            }

            public JoyconSingleArcInputState Left { get; set; }
            public JoyconSingleArcInputState Right { get; set; }

            public JoyconArcInputState(float leftHori, float leftVert, float rightHori, float rightVert)
            {
                Left = new JoyconSingleArcInputState(leftHori, leftVert);
                Right = new JoyconSingleArcInputState(rightHori, rightVert);
            }

            public void Reset()
            {
                Left.Horizontal = 0;
                Left.Vertical = 0;
                Right.Horizontal = 0;
                Right.Vertical = 0;
            }
        }

        public static bool AddIfNotExists<T>(List<T> list, T value)
        {
            if (!list.Contains(value))
            {
                list.Add(value);
                return true;
            }

            return false;
        }

        public record DPadInputState
        {
            public record DPadSingleInputState
            {
                public bool JustClicked { get; set; }
                public float PreviousValue { get; set; }

                public DPadSingleInputState(bool justClicked, float prev)
                {
                    JustClicked = justClicked;
                    PreviousValue = prev;
                }
            }

            public DPadSingleInputState Lane1 { get; }
            public DPadSingleInputState Lane2 { get; }
            public DPadSingleInputState Lane3 { get; }
            public DPadSingleInputState Lane4 { get; }

            public DPadInputState()
            {
                Lane1 = new DPadSingleInputState(false, 0);
                Lane2 = new DPadSingleInputState(false, 0);
                Lane3 = new DPadSingleInputState(false, 0);
                Lane4 = new DPadSingleInputState(false, 0);
            }

            public void Update(DPadSingleInputState lane, float dPadInput)
            {
                lane.JustClicked = dPadInput != lane.PreviousValue;
                lane.PreviousValue = dPadInput;
            }
        }

        public enum TapSide
        {
            Left,
            Right,
            Undefined
        }

        public static TapSide FromLaneIndex(int lane)
        {
            return lane switch
            {
                >= 0 and <= 2 => TapSide.Left,
                <= 5 => TapSide.Right,
                _ => TapSide.Undefined
            };
        }

        public static void LaneFeedback(TapSide side, int timing)
        {
            var enwidened =
                Services.Scenecontrol.Scene.Track.ExtraL.ColorA.ValueAt(timing) >= 200 &&
                Services.Scenecontrol.Scene.Track.ExtraR.ColorA.ValueAt(timing) >= 200;
            switch (side)
            {
                case TapSide.Left:
                    Services.InputFeedback.LaneFeedback(1);
                    Services.InputFeedback.LaneFeedback(2);
                    if (enwidened) Services.InputFeedback.LaneFeedback(0);
                    break;
                case TapSide.Right:
                    Services.InputFeedback.LaneFeedback(3);
                    Services.InputFeedback.LaneFeedback(4);
                    if (enwidened) Services.InputFeedback.LaneFeedback(5);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        protected List<int> CurrentLaneDownInputs = new(4);
        protected List<int> CurrentLaneContinualInputs = new(4);
        protected List<bool> CurrentArctapInputs = new(2);
        protected JoyconArcInputState CurrentArcInputs = new(0, 0, 0, 0);
        protected DPadInputState CurrentDPadInputs = new();

        private static double arcJudgementThreshold = 45.0;
        private static double joystickSensibility = 0.125;

        public void PollInput()
        {
            CurrentLaneDownInputs.Clear();
            CurrentLaneContinualInputs.Clear();
            CurrentArctapInputs.Clear();
            CurrentArcInputs.Reset();

            // Arc Input from Joysticks
            CurrentArcInputs.Left.Horizontal = UnityEngine.Input.GetAxis("Left Horizontal");
            CurrentArcInputs.Left.Vertical = UnityEngine.Input.GetAxis("Left Vertical");
            CurrentArcInputs.Right.Horizontal = UnityEngine.Input.GetAxis("Right Horizontal");
            CurrentArcInputs.Right.Vertical = UnityEngine.Input.GetAxis("Right Vertical");

            // Update D-Pad Input
            float dPadLane1Input = UnityEngine.Input.GetAxis("Lane 1");
            float dPadLane2Input = UnityEngine.Input.GetAxis("Lane 2");
            float dPadLane3AlternateInput = -dPadLane1Input;
            float dPadLane4AlternateInput = -dPadLane2Input;

            CurrentDPadInputs.Update(CurrentDPadInputs.Lane1, dPadLane1Input);
            CurrentDPadInputs.Update(CurrentDPadInputs.Lane2, dPadLane2Input);
            CurrentDPadInputs.Update(CurrentDPadInputs.Lane3, dPadLane3AlternateInput);
            CurrentDPadInputs.Update(CurrentDPadInputs.Lane4, dPadLane4AlternateInput);

            // Normal Button
            if (CurrentDPadInputs.Lane1.JustClicked && dPadLane1Input < 0) AddIfNotExists(CurrentLaneDownInputs, 1);
            if (CurrentDPadInputs.Lane2.JustClicked && dPadLane2Input > 0) AddIfNotExists(CurrentLaneDownInputs, 2);
            if (UnityEngine.Input.GetButtonDown("Lane 3")) AddIfNotExists(CurrentLaneDownInputs, 3);
            if (UnityEngine.Input.GetButtonDown("Lane 4")) AddIfNotExists(CurrentLaneDownInputs, 4);

            if (dPadLane1Input < 0) AddIfNotExists(CurrentLaneContinualInputs, 1);
            if (dPadLane2Input > 0) AddIfNotExists(CurrentLaneContinualInputs, 2);
            if (UnityEngine.Input.GetButton("Lane 3")) AddIfNotExists(CurrentLaneContinualInputs, 3);
            if (UnityEngine.Input.GetButton("Lane 4")) AddIfNotExists(CurrentLaneContinualInputs, 4);

            // Alternate Button
            if (CurrentDPadInputs.Lane3.JustClicked && dPadLane3AlternateInput < 0) AddIfNotExists(CurrentLaneDownInputs, 3);
            if (CurrentDPadInputs.Lane4.JustClicked && dPadLane4AlternateInput > 0) AddIfNotExists(CurrentLaneDownInputs, 4);
            if (UnityEngine.Input.GetButtonDown("Lane 1 Alternate")) AddIfNotExists(CurrentLaneDownInputs, 1);
            if (UnityEngine.Input.GetButtonDown("Lane 2 Alternate")) AddIfNotExists(CurrentLaneDownInputs, 2);

            if (dPadLane3AlternateInput < 0) AddIfNotExists(CurrentLaneContinualInputs, 3);
            if (dPadLane4AlternateInput > 0) AddIfNotExists(CurrentLaneContinualInputs, 4);
            if (UnityEngine.Input.GetButton("Lane 1 Alternate")) AddIfNotExists(CurrentLaneContinualInputs, 1);
            if (UnityEngine.Input.GetButton("Lane 2 Alternate")) AddIfNotExists(CurrentLaneContinualInputs, 2);

            // Arctap
            CurrentArctapInputs.Add(UnityEngine.Input.GetButtonDown("Left Arctap"));
            CurrentArctapInputs.Add(UnityEngine.Input.GetButtonDown("Right Arctap"));

            foreach (var laneInput in CurrentLaneContinualInputs)
            {
                LaneFeedback(FromLaneIndex(laneInput), Services.Audio.AudioTiming);
            }

            // DebugKeyPressed();
        }

        public enum JoyconJudgementSide
        {
            Left,
            Right,
            Middle
        }

        /// <summary>
        /// Modified from [ArcCreate.Gameplay.Skin.JoyconNoteSkinOption.GetArcTapSkin]
        /// </summary>
        public static JoyconJudgementSide GetArcTapJudgementSide(float worldX)
        {
            if (Mathf.Abs(worldX) <= Values.ArcTapMiddleWorldXRange)
            {
                return JoyconJudgementSide.Middle;
            }

            return worldX > 0f ? JoyconJudgementSide.Left : JoyconJudgementSide.Right;
        }

        public void HandleTapRequests(
            int currentTiming,
            UnorderedList<LaneTapJudgementRequest> laneTapRequests,
            UnorderedList<ArcTapJudgementRequest> arcTapRequests)
        {
            for (int inpIndex = 0; inpIndex < CurrentLaneDownInputs.Count; inpIndex++)
            {
                int laneIndex = CurrentLaneDownInputs[inpIndex];
                TapSide side = FromLaneIndex(laneIndex);
                if (side == TapSide.Undefined) continue;

                int minTimingDifference = int.MaxValue;

                bool applicableLaneRequestExists = false;
                LaneTapJudgementRequest applicableLaneRequest = default;
                int applicableLaneRequestIndex = 0;

                for (int i = laneTapRequests.Count - 1; i >= 0; i--)
                {
                    LaneTapJudgementRequest req = laneTapRequests[i];
                    int timingDifference = req.AutoAtTiming - currentTiming;
                    if (timingDifference > minTimingDifference)
                    {
                        continue;
                    }

                    if (FromLaneIndex(req.Lane) == side && timingDifference < minTimingDifference)
                    {
                        minTimingDifference = timingDifference;
                        applicableLaneRequestExists = true;
                        applicableLaneRequest = req;
                        applicableLaneRequestIndex = i;
                    }
                }

                if (applicableLaneRequestExists)
                {
                    applicableLaneRequest.Receiver.ProcessLaneTapJudgement(currentTiming - applicableLaneRequest.AutoAtTiming,
                        applicableLaneRequest.Properties);
                    laneTapRequests.RemoveAt(applicableLaneRequestIndex);
                }
            }

            for (int inpIndex = 0; inpIndex < CurrentArctapInputs.Count; inpIndex++)
            {
                bool input = CurrentArctapInputs[inpIndex];
                if (!input) continue;

                JoyconJudgementSide side;
                if (inpIndex == 0)
                {
                    side = JoyconJudgementSide.Left;
                }
                else
                {
                    side = JoyconJudgementSide.Right;
                }

                int minTimingDifference = int.MaxValue;

                bool applicableArcTapRequestExists = false;
                ArcTapJudgementRequest applicableArcTapRequest = default;
                int applicableArcTapRequestIndex = 0;

                for (int i = arcTapRequests.Count - 1; i >= 0; i--)
                {
                    ArcTapJudgementRequest req = arcTapRequests[i];
                    int timingDifference = req.AutoAtTiming - currentTiming;
                    if (timingDifference > minTimingDifference)
                    {
                        continue;
                    }

                    var targetSide = GetArcTapJudgementSide(req.X);
                    if ((targetSide == side || targetSide == JoyconJudgementSide.Middle) && timingDifference < minTimingDifference)
                    {
                        minTimingDifference = timingDifference;
                        applicableArcTapRequestExists = true;
                        applicableArcTapRequest = req;
                        applicableArcTapRequestIndex = i;
                    }
                }

                if (applicableArcTapRequestExists)
                {
                    applicableArcTapRequest.Receiver.ProcessArcTapJudgement(currentTiming - applicableArcTapRequest.AutoAtTiming,
                        applicableArcTapRequest.Properties);
                    arcTapRequests.RemoveAt(applicableArcTapRequestIndex);
                }
            }
        }

        public void HandleLaneHoldRequests(int currentTiming, UnorderedList<LaneHoldJudgementRequest> requests)
        {
            for (int inpIndex = 0; inpIndex < CurrentLaneContinualInputs.Count; inpIndex++)
            {
                int laneIndex = CurrentLaneContinualInputs[inpIndex];
                TapSide side = FromLaneIndex(laneIndex);
                if (side == TapSide.Undefined) continue;

                for (int i = requests.Count - 1; i >= 0; i--)
                {
                    LaneHoldJudgementRequest req = requests[i];

                    if (currentTiming < req.StartAtTiming)
                    {
                        continue;
                    }

                    if (FromLaneIndex(req.Lane) == side)
                    {
                        req.Receiver.ProcessLaneHoldJudgement(currentTiming >= req.ExpireAtTiming, req.IsJudgement, req.Properties);
                        requests.RemoveAt(i);
                    }
                }
            }
        }

        private float MatrixToEuler(Matrix4x4 matrix)
        {
            Vector3 forward = new Vector3(matrix.GetColumn(2).x, matrix.GetColumn(2).y, matrix.GetColumn(2).z);
            Vector3 upwards = new Vector3(matrix.GetColumn(1).x, matrix.GetColumn(1).y, matrix.GetColumn(1).z);

            if (forward == Vector3.zero)
            {
                forward = Vector3.forward;
            }

            if (upwards == Vector3.zero)
            {
                upwards = Vector3.up;
            }

            Quaternion rotation = Quaternion.LookRotation(forward, upwards);
            return rotation.eulerAngles[2];
        }

        private static double GetAngleDeviation(double input, double judge)
        {
            var d = Math.Abs(judge - input);
            return Math.Min(d, 360 - d);
        }

        private static double CalculateEulerFromJoystickInput(JoyconArcInputState.JoyconSingleArcInputState input)
        {
            float h = input.Horizontal;
            float v = input.Vertical;
            if (Math.Sqrt(Math.Pow(h, 2) + Math.Pow(v, 2)) < joystickSensibility) return -1;
            var radians = Math.PI / 2 - Math.Atan(v / h);
            if (h < 0) radians += Math.PI;
            return 180 / Math.PI * radians;
        }

        public void HandleArcRequests(int currentTiming, UnorderedList<ArcJudgementRequest> requests)
        {
            JoyconArcInputState input = CurrentArcInputs;

            for (int i = requests.Count - 1; i >= 0; i--)
            {
                ArcJudgementRequest req = requests[i];
                if (currentTiming < req.StartAtTiming)
                {
                    continue;
                }

                bool accepted;

                if (req.Arc.XStart == req.Arc.XEnd && req.Arc.YStart == req.Arc.YEnd)
                {
                    accepted = true;
                }
                else
                {
                    var matrix = req.Arc.ArcCapMatrix;
                    var eulerAngles = MatrixToEuler(matrix);

                    double currentAngle = req.Arc.Color switch
                    {
                        0 => CalculateEulerFromJoystickInput(input.Left),
                        1 => CalculateEulerFromJoystickInput(input.Right),
                        _ => -1.0
                    };

                    double deviation = GetAngleDeviation(currentAngle, eulerAngles);
                    accepted = currentAngle <= 0 && deviation < arcJudgementThreshold;
                }

                if (accepted)
                {
                    req.Receiver.ProcessArcJudgement(currentTiming >= req.ExpireAtTiming, req.IsJudgement, req.Properties);
                    requests.RemoveAt(i);
                }
            }
        }

        public void ResetJudge()
        {
        }
    }
}