using System;
using System.Collections.Generic;
using ArcCreate.Gameplay.Data;
using ArcCreate.Gameplay.Skin;
using ArcCreate.Utility;
using JetBrains.Annotations;
using UnityEngine;
using NoteSide = ArcCreate.Gameplay.Skin.JoyconNoteSkinOption.JoyconJudgementSide;

namespace ArcCreate.Gameplay.Judgement.Input
{
    public class ControllerInputHandler : IInputHandler
    {
        public record JoyconArcInputState
        {
            public record JoyconSingleArcInputState(float Horizontal, float Vertical, NoteSide Side)
            {
                public float Horizontal { get; set; } = Horizontal;
                public float Vertical { get; set; } = Vertical;

                public NoteSide Side { get; } = Side;

                public (float, float) GetAxises()
                {
                    return (Horizontal, Vertical);
                }

                public Quaternion? CalculateQuaternionFromInput()
                {
                    var (h, v) = GetAxises();
                    if (!IsJoystickInputValid(this)) return null;

                    var radians = Mathf.PI / 2 - Mathf.Atan(v / h);
                    if (h < 0) radians += Mathf.PI;
                    return Quaternion.Euler(0, 0, radians * Mathf.Rad2Deg);
                }
            }

            public JoyconSingleArcInputState Left { get; }
            public JoyconSingleArcInputState Right { get; }

            public JoyconArcInputState(float leftHori, float leftVert, float rightHori, float rightVert)
            {
                Left = new JoyconSingleArcInputState(leftHori, leftVert, NoteSide.Left);
                Right = new JoyconSingleArcInputState(rightHori, rightVert, NoteSide.Right);
            }

            public void Reset()
            {
                Left.Horizontal = 0;
                Left.Vertical = 0;
                Right.Horizontal = 0;
                Right.Vertical = 0;
            }

            [CanBeNull]
            public JoyconSingleArcInputState GetInputStateForColor(int color)
            {
                switch (color)
                {
                    case 0:
                        return Left;
                    case 1:
                        return Right;

                    default:
                        return null;
                }
            }
        }

        private static bool AddIfNotExists<T>(ICollection<T> list, T value)
        {
            if (list.Contains(value)) return false;
            list.Add(value);
            return true;
        }

        public record DPadInputState
        {
            public record LinearSingleInputState(bool JustClicked, float PreviousValue)
            {
                public bool JustClicked { get; set; } = JustClicked;
                public float PreviousValue { get; set; } = PreviousValue;
            }

            public LinearSingleInputState Lane1 { get; } = new(false, 0);
            public LinearSingleInputState Lane2 { get; } = new(false, 0);
            public LinearSingleInputState Lane3 { get; } = new(false, 0);
            public LinearSingleInputState Lane4 { get; } = new(false, 0);
            public LinearSingleInputState TriggerLeft { get; } = new(false, 0);
            public LinearSingleInputState TriggerRight { get; } = new(false, 0);

            public void Update(LinearSingleInputState lane, float dPadInput)
            {
                lane.JustClicked = !Mathf.Approximately(dPadInput, lane.PreviousValue);
                lane.PreviousValue = dPadInput;
            }
        }

        public static NoteSide FromLaneIndex(int lane)
        {
            return JoyconNoteSkinOption.GetSideFromLaneIndex(lane);
        }

        public static void LaneFeedback(NoteSide side)
        {
            switch (side)
            {
                case NoteSide.Left:
                    for (int i = (int)Values.LaneFrom; i <= 2; i++)
                    {
                        Services.InputFeedback.LaneFeedback(i);
                    }

                    break;
                case NoteSide.Right:
                    for (int i = 3; i <= Values.LaneTo; i++)
                    {
                        Services.InputFeedback.LaneFeedback(i);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        protected List<NoteSide> CurrentNoteDownInputs = new(4);
        protected List<NoteSide> CurrentNoteContinualInputs = new(4);
        public JoyconArcInputState CurrentArcInputs = new(0, 0, 0, 0);
        protected DPadInputState CurrentLinearInputs = new();

        private const double JoyconArcJudgementThreshold = 40.0;
        private const double JoyconArcActiveCorrectionThreshold = 90 - 22.5;
        private const double JoyconArcSensibility = 0.125;

        public void PollInput()
        {
            CurrentNoteDownInputs.Clear();
            CurrentNoteContinualInputs.Clear();
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
            float triggerLeftInput = UnityEngine.Input.GetAxis("Left Arctap Alternate");
            float triggerRightInput = UnityEngine.Input.GetAxis("Right Arctap Alternate");

            CurrentLinearInputs.Update(CurrentLinearInputs.Lane1, dPadLane1Input);
            CurrentLinearInputs.Update(CurrentLinearInputs.Lane2, dPadLane2Input);
            CurrentLinearInputs.Update(CurrentLinearInputs.Lane3, dPadLane3AlternateInput);
            CurrentLinearInputs.Update(CurrentLinearInputs.Lane4, dPadLane4AlternateInput);
            CurrentLinearInputs.Update(CurrentLinearInputs.TriggerLeft, triggerLeftInput);
            CurrentLinearInputs.Update(CurrentLinearInputs.TriggerRight, triggerRightInput);

            // Arctap
            if (UnityEngine.Input.GetButtonDown("Left Arctap")) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Left);
            if (UnityEngine.Input.GetButtonDown("Right Arctap")) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Right);
            if (CurrentLinearInputs.TriggerLeft.JustClicked && triggerLeftInput > 0.5) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Left);
            if (CurrentLinearInputs.TriggerRight.JustClicked && triggerRightInput > 0.5)
                AddIfNotExists(CurrentNoteDownInputs, NoteSide.Right);

            if (UnityEngine.Input.GetButton("Left Arctap")) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Left);
            if (UnityEngine.Input.GetButton("Right Arctap")) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Right);
            if (triggerLeftInput > 0.5) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Left);
            if (triggerRightInput > 0.5) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Right);

            // Normal Button
            if (CurrentLinearInputs.Lane1.JustClicked && dPadLane1Input < 0) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Left);
            if (CurrentLinearInputs.Lane2.JustClicked && dPadLane2Input > 0) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Left);
            if (UnityEngine.Input.GetButtonDown("Lane 3")) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Right);
            if (UnityEngine.Input.GetButtonDown("Lane 4")) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Right);

            if (dPadLane1Input < 0) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Left);
            if (dPadLane2Input > 0) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Left);
            if (UnityEngine.Input.GetButton("Lane 3")) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Right);
            if (UnityEngine.Input.GetButton("Lane 4")) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Right);

            // Alternate Button
            if (CurrentLinearInputs.Lane3.JustClicked && dPadLane3AlternateInput < 0) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Right);
            if (CurrentLinearInputs.Lane4.JustClicked && dPadLane4AlternateInput > 0) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Right);
            if (UnityEngine.Input.GetButtonDown("Lane 1 Alternate")) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Left);
            if (UnityEngine.Input.GetButtonDown("Lane 2 Alternate")) AddIfNotExists(CurrentNoteDownInputs, NoteSide.Left);

            if (dPadLane3AlternateInput < 0) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Right);
            if (dPadLane4AlternateInput > 0) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Right);
            if (UnityEngine.Input.GetButton("Lane 1 Alternate")) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Left);
            if (UnityEngine.Input.GetButton("Lane 2 Alternate")) AddIfNotExists(CurrentNoteContinualInputs, NoteSide.Left);

            foreach (var laneInput in CurrentNoteContinualInputs)
            {
                LaneFeedback(laneInput);
            }

            // DebugKeyPressed();
        }

        public void HandleTapRequests(
            int currentTiming,
            UnorderedList<LaneTapJudgementRequest> laneTapRequests,
            UnorderedList<ArcTapJudgementRequest> arcTapRequests)
        {
            for (int inpIndex = 0; inpIndex < CurrentNoteDownInputs.Count; inpIndex++)
            {
                NoteSide side = CurrentNoteDownInputs[inpIndex];
                if (side == NoteSide.Undefined) continue;

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

                    var targetSide = JoyconNoteSkinOption.GetArcTapJudgementSide(req.X, req.Width);
                    if ((targetSide == side || targetSide == NoteSide.Middle) &&
                        timingDifference < minTimingDifference)
                    {
                        minTimingDifference = timingDifference;
                        applicableArcTapRequestExists = true;
                        applicableArcTapRequest = req;
                        applicableArcTapRequestIndex = i;
                    }
                }


                if (applicableLaneRequestExists)
                {
                    applicableLaneRequest.Receiver.ProcessLaneTapJudgement(currentTiming - applicableLaneRequest.AutoAtTiming,
                        applicableLaneRequest.Properties);
                    laneTapRequests.RemoveAt(applicableLaneRequestIndex);
                }
                else if (applicableArcTapRequestExists)
                {
                    applicableArcTapRequest.Receiver.ProcessArcTapJudgement(currentTiming - applicableArcTapRequest.AutoAtTiming,
                        applicableArcTapRequest.Properties);
                    arcTapRequests.RemoveAt(applicableArcTapRequestIndex);
                }
            }
        }

        public void HandleLaneHoldRequests(int currentTiming, UnorderedList<LaneHoldJudgementRequest> requests)
        {
            for (int inpIndex = 0; inpIndex < CurrentNoteContinualInputs.Count; inpIndex++)
            {
                NoteSide side = CurrentNoteContinualInputs[inpIndex];
                if (side == NoteSide.Undefined) continue;

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

        private static float GetAngleDeviation(float input, float judge)
        {
            var d = Mathf.Abs(judge - input);
            return Mathf.Min(d, 360 - d);
        }

        private static bool IsJoystickInputValid(JoyconArcInputState.JoyconSingleArcInputState input)
        {
            var (h, v) = input.GetAxises();
            if (Mathf.Sqrt(Mathf.Pow(h, 2) + Mathf.Pow(v, 2)) < JoyconArcSensibility) return false;
            return true;
        }

        private static bool IsArcConnectedLooselessly(Arc prev, Arc next)
        {
            if (prev == null || next == null) return false;
            return prev.EndTiming == next.Timing &&
                   Mathf.Approximately(next.XStart, prev.XEnd) &&
                   Mathf.Approximately(next.YStart, prev.YEnd);
        }

        private static bool IsArcConnectedLoosely(Arc prev, Arc next)
        {
            if (prev == null || next == null) return false;
            return Mathf.Abs(next.Timing - prev.EndTiming) < 10 &&
                   Mathf.Abs(next.XStart - prev.XEnd) < 0.1 &&
                   Mathf.Abs(next.YStart - prev.YEnd) < 0.1;
        }

        public Dictionary<int, KeyValuePair<Arc, bool>> PreviousArcStates = new();

        public void HandleArcRequests(int currentTiming, UnorderedList<ArcJudgementRequest> requests)
        {
            JoyconArcInputState input = CurrentArcInputs;
            ArcJoyconColorLogic.NewFrame(currentTiming);

            // Notify if arcs exists
            for (int c = 0; c <= ArcJoyconColorLogic.MaxColor; c++)
            {
                ArcJoyconColorLogic logic = ArcJoyconColorLogic.Get(c);

                bool arcOfColorExists = false;
                for (int i = requests.Count - 1; i >= 0; i--)
                {
                    ArcJudgementRequest req = requests[i];
                    if (currentTiming >= req.StartAtTiming
                        && currentTiming <= req.Arc.EndTiming
                        && req.Arc.Color == logic.Color)
                    {
                        arcOfColorExists = true;
                        break;
                    }
                }

                logic.ExistsArcWithinRange(arcOfColorExists);
            }

            for (int i = requests.Count - 1; i >= 0; i--)
            {
                ArcJudgementRequest req = requests[i];
                ArcJoyconColorLogic logic = ArcJoyconColorLogic.Get(req.Arc.Color);
                if (currentTiming < req.StartAtTiming)
                {
                    continue;
                }

                JoyconArcInputState.JoyconSingleArcInputState singleInput = input.GetInputStateForColor(req.Arc.Color);

                if (AbnormalColorJudge(singleInput))
                {
                    // `singleInput` is asserted to be not null below
                    goto JudgementPassed;
                }

                if (DirectionJudge(currentTiming, singleInput, req.Arc, logic) && !logic.IsInputLocked)
                {
                    goto JudgementPassed;
                }

                goto FunctionEnd;

                JudgementPassed:
                req.Receiver.ProcessArcJudgement(currentTiming >= req.ExpireAtTiming, req.IsJudgement, req.Properties);
                requests.RemoveAt(i);
            }

            FunctionEnd:
            ArcJoyconColorLogic.ApplyRedValue();
        }

        public void ResetJudge()
        {
            PreviousArcStates.Clear();
            ArcJoyconColorLogic.ResetAll();
        }

        private bool AbnormalColorJudge(JoyconArcInputState.JoyconSingleArcInputState singleInput)
        {
            if (singleInput is null)
            {
                // Directly accept arcs with color other than 0 or 1
                return true;
            }

            return false;
        }

        private bool DirectionJudge(int currentTiming, JoyconArcInputState.JoyconSingleArcInputState singleInput, Arc arc,
            ArcJoyconColorLogic logic)
        {
            bool accepted;

            var segment = arc.SegmentAt(currentTiming);
            if (segment is null) return false;

            var rot = Arc.CalculateArcCapRotation(segment.Value);

            // Directly accept straight arcs
            if ((Mathf.Approximately(arc.XStart, arc.XEnd)
                 && Mathf.Approximately(arc.YStart, arc.YEnd))
                || rot == Quaternion.identity)
            {
                return true;
            }

            var inputRot = singleInput.CalculateQuaternionFromInput();
            if (inputRot is null) return false;

            float inputAngle = inputRot.Value.eulerAngles[2];
            var arcCapAngle = rot.eulerAngles[2];

            float deviation = GetAngleDeviation(inputAngle, arcCapAngle);

            accepted = deviation < JoyconArcJudgementThreshold;

            // If not accepted, try fix
            if (!accepted)
            {
                if (PreviousArcStates.ContainsKey(arc.Color))
                {
                    var previousArc = PreviousArcStates[arc.Color].Key;
                    var previousAccepted = PreviousArcStates[arc.Color].Value;

                    if (IsJoystickInputValid(singleInput))
                    {
                        // Check if the current arc is seamlessly connected to the previous one
                        if (previousAccepted && IsArcConnectedLoosely(previousArc, arc))
                        {
                            // Perform the fix
                            var previousSegment =
                                previousArc.SegmentAt(previousArc.EndTiming); // get last direction of previous arc's cap
                            if (previousSegment is null) return false;

                            var previousArcRot = Arc.CalculateArcCapRotation(previousSegment.Value);

                            float previousArcCapAngle = previousArcRot.eulerAngles[2];
                            float deviationBetweenPrev = GetAngleDeviation(arcCapAngle, previousArcCapAngle);

                            if (deviationBetweenPrev > JoyconArcActiveCorrectionThreshold)
                            {
                                return true;
                            }
                        }
                        else if (!previousAccepted
                                 && IsArcConnectedLoosely(previousArc, arc)
                                 && deviation > 180 - JoyconArcJudgementThreshold)
                        {
                            // Red coloring if confirmed as mis-input
                            logic.LockInput((float)arc.TimeIncrement);
                        }
                    }
                }
            }


            if (!PreviousArcStates.ContainsKey(arc.Color))
            {
                PreviousArcStates.Add(arc.Color, KeyValuePair.Create(arc, accepted));
            }
            else
            {
                PreviousArcStates[arc.Color] = KeyValuePair.Create(arc, accepted);
            }

            return accepted;
        }
    }
}