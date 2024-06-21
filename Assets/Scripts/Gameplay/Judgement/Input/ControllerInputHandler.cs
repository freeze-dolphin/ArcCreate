using System;
using System.Collections.Generic;
using ArcCreate.Utility;
using UnityEngine;

namespace ArcCreate.Gameplay.Judgement.Input
{
    public class ControllerInputHandler : IInputHandler
    {
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

        protected List<int> CurrentLaneDownInputs = new(4);
        protected List<int> CurrentLaneContinualInputs = new(4);
        protected List<bool> CurrentArctapInputs = new(2);
        protected JoyconArcInputState CurrentArcInputs = new(0, 0, 0, 0);

        private double arcJudgementThreshold = 90;

        private bool lane1JustClicked;
        private float prevLane1;
        private bool lane2JustClicked;
        private float prevLane2;

        public void PollInput()
        {
            CurrentLaneDownInputs.Clear();
            CurrentLaneContinualInputs.Clear();
            CurrentArctapInputs.Clear();
            CurrentArcInputs.Reset();

            CurrentArcInputs.Left.Horizontal = UnityEngine.Input.GetAxis("Left Horizontal");
            CurrentArcInputs.Left.Vertical = UnityEngine.Input.GetAxis("Left Vertical");
            CurrentArcInputs.Right.Horizontal = UnityEngine.Input.GetAxis("Right Horizontal");
            CurrentArcInputs.Right.Vertical = UnityEngine.Input.GetAxis("Right Vertical");

            float lane1 = UnityEngine.Input.GetAxis("Lane 1");
            float lane2 = UnityEngine.Input.GetAxis("Lane 2");

            lane1JustClicked = lane1 != prevLane1;
            lane2JustClicked = lane2 != prevLane2;

            if (lane1JustClicked && lane1 < 0) CurrentLaneDownInputs.Add(1);
            if (lane2JustClicked && lane2 > 0) CurrentLaneDownInputs.Add(2);
            if (UnityEngine.Input.GetButtonDown("Lane 3")) CurrentLaneDownInputs.Add(3);
            if (UnityEngine.Input.GetButtonDown("Lane 4")) CurrentLaneDownInputs.Add(4);

            if (lane1 < 0) CurrentLaneContinualInputs.Add(1);
            if (lane2 > 0) CurrentLaneContinualInputs.Add(2);
            if (UnityEngine.Input.GetButton("Lane 3")) CurrentLaneContinualInputs.Add(3);
            if (UnityEngine.Input.GetButton("Lane 4")) CurrentLaneContinualInputs.Add(4);

            CurrentArctapInputs.Add(UnityEngine.Input.GetButtonDown("Left Arctap"));
            CurrentArctapInputs.Add(UnityEngine.Input.GetButtonDown("Right Arctap"));

            for (int i = 0; i < CurrentLaneContinualInputs.Count; i++)
            {
                Services.InputFeedback.LaneFeedback(CurrentLaneContinualInputs[i]);
            }

            prevLane1 = lane1;
            prevLane2 = lane2;
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
                int lane = CurrentLaneDownInputs[inpIndex];

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

                    if (req.Lane == lane && timingDifference < minTimingDifference)
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
                int lane = CurrentLaneContinualInputs[inpIndex];

                for (int i = requests.Count - 1; i >= 0; i--)
                {
                    LaneHoldJudgementRequest req = requests[i];

                    if (currentTiming < req.StartAtTiming)
                    {
                        continue;
                    }

                    if (req.Lane == lane)
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
            return Math.Min(
                Math.Abs(input - judge),
                Math.Abs(judge - input)
            );
        }

        private static double CalculateEulerFromJoystickInput(JoyconArcInputState.JoyconSingleArcInputState input)
        {
            float h = input.Horizontal;
            float v = input.Vertical;
            if (Math.Sqrt(Math.Pow(h, 2) + Math.Pow(v, 2)) < 0.125) return -1;
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

                var matrix = req.Arc.ArcCapMatrix;
                var eulerAngles = MatrixToEuler(matrix);

                double currentAngle;

                switch (req.Arc.Color)
                {
                    case 0:
                        currentAngle = CalculateEulerFromJoystickInput(input.Left);
                        break;

                    case 1:
                        currentAngle = CalculateEulerFromJoystickInput(input.Right);
                        break;

                    default:
                        currentAngle = -1;
                        break;
                }

                double deviation = GetAngleDeviation(currentAngle, eulerAngles);
                bool accepted = currentAngle >= 0 && deviation < arcJudgementThreshold;

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