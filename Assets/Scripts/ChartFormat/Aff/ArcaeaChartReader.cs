using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using ArcCreate.ChartFormat.Grammar;
using ArcCreate.Utility.Parser;
using UnityEngine;

namespace ArcCreate.ChartFormat
{
    /// <summary>
    /// Object for reading a .aff chart file.
    /// </summary>
    public class ArcaeaChartReader : ChartReader
    {
        public ArcaeaChartReader(IFileAccessWrapper fileAccess, string relativeDirectory, string fullPath, string filename)
            : base(fileAccess, relativeDirectory, fullPath, filename)
        {
            TimingPointDensity = 1;
            AudioOffset = 0;
        }

        public override Result<ChartFileErrors> Parse()
        {
            var errors = new List<ChartError>();

            TimingGroups.Add(new RawTimingGroup() { File = Filename });
            AllIncludes.Add(Filename);

            var lines = FileAccess.ReadFileByLines(FullPath);
            if (!lines.HasValue)
            {
                errors.Add(ChartError.Format(RawEventType.Unknown, ChartError.Kind.FileDoesNotExist));
                return new ChartFileErrors(Filename, errors);
            }

            #region Header

            if (!ParseHeader(lines.Value).TryUnwrap(out var headerParseResult, out var error))
            {
                errors.Add(error);
                return new ChartFileErrors(Filename, errors);
            }

            var (headerLineNumber, headerDict) = headerParseResult;

            int audioOffset = AudioOffset;
            if (headerDict.TryGetValue("AudioOffset", out string audioOffsetRaw))
            {
                Evaluator.TryInt(audioOffsetRaw, out audioOffset);
            }

            AudioOffset = audioOffset;

            float density = TimingPointDensity;
            if (headerDict.TryGetValue("TimingPointsDensityFactor", out var tpdfRaw) ||
                headerDict.TryGetValue("TimingPointDensityFactor", out tpdfRaw))
            {
                Evaluator.TryFloat(tpdfRaw, out density);
            }

            TimingPointDensity = density;

            #endregion

            var antlrInput = new AntlrInputStream(string.Join("\n", lines.Value.Skip(headerLineNumber)));
            var lexer = new UniversalAffChartLexer(antlrInput);
            var tokens = new CommonTokenStream(lexer);
            var parser = new UniversalAffChartParser(tokens);

            parser.RemoveErrorListeners();
            parser.AddErrorListener(new AntlrChartErrorListener(lines.Value));

            var visitor = new UniversalChartVisitor();

            try
            {
                var chartSegment = (AntlrEventSegment)visitor.VisitChart(parser.chart());

                foreach (var evt in chartSegment.Events)
                {
                    if (evt.Name == "timinggroup")
                    {
                        var (tg, events) = ParseTimingGroup(evt, TimingGroups.Count);
                        TimingGroups.Add(tg);
                        Events.AddRange(events);
                    }
                    else
                    {
                        Events.Add(ParseEvent(evt, 0));
                    }
                }
            }
            catch (AntlrParseException ex)
            {
                errors.Add(ChartError.Parsing(ex.Raw, ex.LineNumber, ex.EventType,
                    new ParsingError(ex.Message, 0, ex.Raw.Length, ParsingError.Kind.Antlr)));
            }
            catch (ChartReaderException ex)
            {
                errors.Add(ChartError.Property(ex.Raw, ex.LineNumber, ex.EventType, 0, ex.Raw.Length, ChartError.Kind.Parsing));
            }

            foreach (ChartReader reference in References)
            {
                int removedBaseGroup = 0;
                int referenceBaseGroupCount = reference.Events.Count(e => e.TimingGroup == 0);

                if (referenceBaseGroupCount <= 1)
                {
                    reference.TimingGroups.RemoveAt(0);
                    removedBaseGroup = 1;
                    for (int i = reference.Events.Count - 1; i >= 0; i--)
                    {
                        if (reference.Events[i].TimingGroup == 0)
                        {
                            reference.Events.RemoveAt(i);
                        }
                    }
                }

                foreach (RawEvent e in reference.Events)
                {
                    e.TimingGroup += TimingGroups.Count - removedBaseGroup;
                }

                Events.AddRange(reference.Events);
                TimingGroups.AddRange(reference.TimingGroups);
            }

            var validation = FinalValidity();
            if (validation.IsError) errors.Add(validation.Error);

            Events.Sort((a, b) => a.Timing.CompareTo(b.Timing));

            return errors.Count > 0
                ? new ChartFileErrors(Filename, errors)
                : Result<ChartFileErrors>.Ok();
        }

        public override Result<(int startLine, Dictionary<string, string>), ChartError> ParseHeader(string[] lines)
        {
            var headerItems = new Dictionary<string, string>();

            for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                var line = lines[lineNumber];
                if (line.StartsWith('-'))
                {
                    return (lineNumber, headerItems);
                }

                StringParser s = new StringParser(line);
                if (!s.ReadString(":").TryUnwrap(out TextSpan<string> headerType, out ParsingError e) ||
                    !s.ReadString().TryUnwrap(out var value, out e))
                {
                    return ChartError.Parsing(line, lineNumber, RawEventType.Header, e);
                }

                headerItems.Add(headerType, value);
            }

            return ChartError.Parsing("-", 0, RawEventType.Header,
                new ParsingError("No header found", 0, lines[0].Length, ParsingError.Kind.CharacterNotFound));
        }

        public override RawTimingGroup ParseTimingGroupProperties(string raw, AntlrEvent evt)
        {
            var propDict = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var antlrValue in evt.Values)
                {
                    if (antlrValue.Type == AntlrValueType.String)
                    {
                        propDict.Add(antlrValue.GetStringValue(), null);
                    }

                    else
                    {
                        var (key, aValue) = antlrValue.GetKeyValuePair();
                        if (aValue.HasKeyValuePair)
                        {
                            propDict.Add(key, aValue.GetStringValue());
                        }
                        else if (aValue.HasAlgebraicValue)
                        {
                            propDict.Add(key, aValue.GetAlgebraicValue().ToString(CultureInfo.InvariantCulture));
                        }

                        throw new AntlrParseException("Recursive key-value pair is not allowed", evt.Raw,
                            RawEventType.AntlrValue, evt.LineNumber, evt.ColumnNumber);
                    }
                }
            }

            var prop = new RawTimingGroup();

            foreach (var (type, value) in propDict)
            {
                if (value != null)
                {
                    bool valid;
                    float val;
                    switch (type.ToLower())
                    {
                        case "name":
                            prop.Name = value.Trim('"');
                            break;
                        case "anglex":
                            valid = Evaluator.TryFloat(value, out val);
                            prop.AngleX = valid ? val : 0;
                            break;
                        case "angley":
                            valid = Evaluator.TryFloat(value, out val);
                            prop.AngleY = valid ? val : 0;
                            break;
                        case "judgesizex":
                            valid = Evaluator.TryFloat(value, out val);
                            prop.JudgementSizeX = valid ? val : 1;
                            break;
                        case "judgesizey":
                            valid = Evaluator.TryFloat(value, out val);
                            prop.JudgementSizeY = valid ? val : 1;
                            break;
                        case "judgeoffsetx":
                            valid = Evaluator.TryFloat(value, out val);
                            prop.JudgementOffsetX = valid ? val : 1;
                            break;
                        case "judgeoffsety":
                            valid = Evaluator.TryFloat(value, out val);
                            prop.JudgementOffsetY = valid ? val : 1;
                            break;
                        case "judgeoffsetz":
                            valid = Evaluator.TryFloat(value, out val);
                            prop.JudgementOffsetZ = valid ? val : 1;
                            break;
                        case "arcresolution":
                            valid = Evaluator.TryFloat(value, out val);
                            val = Mathf.Clamp(val, 0.1f, 10);
                            prop.ArcResolution = valid ? val : 1;
                            break;
                        case "droprate":
                            valid = Evaluator.TryFloat(value, out val);
                            prop.DropRate = valid ? val : 0;
                            break;
                        case "max":
                            prop.AddRemapRules(value, JudgementMap.Max);
                            break;
                        case "perfect":
                            prop.AddRemapRules(value, JudgementMap.PerfectEarly, JudgementMap.PerfectLate);
                            break;
                        case "perfectearly":
                            prop.AddRemapRules(value, JudgementMap.PerfectEarly);
                            break;
                        case "perfectlate":
                            prop.AddRemapRules(value, JudgementMap.PerfectLate);
                            break;
                        case "good":
                            prop.AddRemapRules(value, JudgementMap.GoodEarly, JudgementMap.GoodLate);
                            break;
                        case "goodearly":
                            prop.AddRemapRules(value, JudgementMap.GoodEarly);
                            break;
                        case "goodlate":
                            prop.AddRemapRules(value, JudgementMap.GoodLate);
                            break;
                        case "miss":
                            prop.AddRemapRules(value, JudgementMap.MissEarly, JudgementMap.MissLate);
                            break;
                        case "missearly":
                            prop.AddRemapRules(value, JudgementMap.MissEarly);
                            break;
                        case "misslate":
                            prop.AddRemapRules(value, JudgementMap.MissLate);
                            break;
                        default:
                            throw new ChartReaderException(raw, RawEventType.TimingGroup, evt,
                                ChartError.Kind.TimingGroupPropertiesInvalid);
                    }
                }
                else
                {
                    switch (type.ToLower())
                    {
                        case "noinput":
                            prop.NoInput = true;
                            break;
                        case "noclip":
                            prop.NoClip = true;
                            break;
                        case "noheightindicator":
                            prop.NoHeightIndicator = true;
                            break;
                        case "nohead":
                            prop.NoHead = true;
                            break;
                        case "noshadow":
                            prop.NoShadow = true;
                            break;
                        case "noarccap":
                            prop.NoArcCap = true;
                            break;
                        case "noconnection":
                            prop.NoConnection = true;
                            break;
                        case "light":
                            prop.Side = SideOverride.Light;
                            break;
                        case "conflict":
                            prop.Side = SideOverride.Conflict;
                            break;
                        case "fadingholds":
                            prop.FadingHolds = true;
                            break;
                        case "ignoremirror":
                            prop.IgnoreMirror = true;
                            break;
                        case "autoplay":
                            prop.Autoplay = true;
                            break;
                        default:
                            throw new ChartReaderException(raw, RawEventType.TimingGroup, evt,
                                ChartError.Kind.TimingGroupPropertiesInvalid);
                    }
                }
            }

            return prop;
        }

        public override RawEvent ParseEvent(AntlrEvent evt, int timingGroup) => evt.Name switch
        {
            null or "" => ParseTap(evt, timingGroup),
            "hold" => ParseHold(evt, timingGroup),
            "timing" => ParseTiming(evt, timingGroup),
            "arc" => ParseArc(evt, timingGroup),
            "scenecontrol" => ParseSceneControl(evt, timingGroup),
            "camera" => ParseCamera(evt, timingGroup),
            "include" => ParseInclude(evt),
            "fragment" => ParseFragment(evt),

            _ => throw new ChartReaderException(evt.Raw, RawEventType.Unknown, evt, ChartError.Kind.Parsing)
        };

        protected virtual RawTap ParseTap(AntlrEvent evt, int timingGroup)
        {
            var validator = new ChartReaderValidator(evt, RawEventType.Tap);

            validator.Require(evt.Values.Count == 2);
            validator.Require(evt.Values[0].TryGetAlgebraicValue(out var tick));
            validator.Require(evt.Values[1].TryGetAlgebraicValue(out var lane));

            return new RawTap
            {
                Timing = (int)tick,
                Lane = (float)lane,
                Type = RawEventType.Tap,
                TimingGroup = timingGroup,
                Line = evt.LineNumber
            };
        }

        protected virtual RawHold ParseHold(AntlrEvent evt, int timingGroup)
        {
            var validator = new ChartReaderValidator(evt, RawEventType.Hold);

            validator.Require(evt.Values.Count == 3);
            validator.Require(evt.Values[0].TryGetAlgebraicValue(out var tick));
            validator.Require(evt.Values[1].TryGetAlgebraicValue(out var endTick));
            validator.Require(evt.Values[2].TryGetAlgebraicValue(out var track));

            if (endTick <= tick) throw new ChartReaderException(evt.Raw, RawEventType.Hold, evt, ChartError.Kind.DurationNegative);

            return new RawHold
            {
                Timing = (int)tick,
                EndTiming = (int)endTick,
                Lane = (float)track,
                Type = RawEventType.Hold,
                TimingGroup = timingGroup,
                Line = evt.LineNumber
            };
        }

        protected virtual RawTiming ParseTiming(AntlrEvent evt, int timingGroup)
        {
            var validator = new ChartReaderValidator(evt, RawEventType.Timing);

            validator.Require(evt.Values.Count == 3);
            validator.Require(evt.Values[0].TryGetAlgebraicValue(out var tick));
            validator.Require(evt.Values[1].TryGetAlgebraicValue(out var bpm));
            validator.Require(evt.Values[2].TryGetAlgebraicValue(out var divisor) && divisor >= 0, ChartError.Kind.DivisorNegative);

            return new RawTiming
            {
                Timing = (int)tick,
                Divisor = (float)divisor,
                Bpm = (float)bpm,
                Type = RawEventType.Timing,
                TimingGroup = timingGroup,
                Line = evt.LineNumber
            };
        }

        protected virtual RawArc ParseArc(AntlrEvent evt, int timingGroup)
        {
            var validator = new ChartReaderValidator(evt, RawEventType.Arc);

            validator.Require(evt.Values.Count is >= 10 and <= 11);
            validator.Require(evt.Values[0].TryGetAlgebraicValue(out var tick));
            validator.Require(evt.Values[1].TryGetAlgebraicValue(out var endTick));
            validator.Require(evt.Values[2].TryGetAlgebraicValue(out var xStart));
            validator.Require(evt.Values[3].TryGetAlgebraicValue(out var xEnd));
            validator.Require(evt.Values[4].TryGetStringValue(out var lineType));
            validator.Require(evt.Values[5].TryGetAlgebraicValue(out var yStart));
            validator.Require(evt.Values[6].TryGetAlgebraicValue(out var yEnd));
            validator.Require(evt.Values[7].TryGetAlgebraicValue(out var color) && color >= 0, ChartError.Kind.ArcColorNegative);
            validator.Require(evt.Values[8].TryGetStringValue(out var hitSound));
            validator.Require(evt.Values[9].TryGetStringValue(out var arcType));

            if (endTick < tick) throw new ChartReaderException(evt.Raw, RawEventType.Arc, evt, ChartError.Kind.DurationNegative);

            double arcResolution = 1.0;
            if (evt.Properties.TryGetValue(RawArc.ArcResolutionKey, out var arcResolutionRaw) &&
                arcResolutionRaw.TryGetAlgebraicValue(out arcResolution)) // try get arcResolution from properties first
            {
            }
            else
            {
                // if not presented in properties, try parse from Arc parameters
                if (evt.Values.Count >= 11) evt.Values[10].TryGetAlgebraicValue(out arcResolution);
            }

            var isTrace = arcType is "true" or "designant";

            return new RawArc
            {
                Timing = (int)tick,
                EndTiming = (int)endTick,
                XStart = (float)xStart,
                XEnd = (float)xEnd,
                LineType = lineType,
                YStart = (float)yStart,
                YEnd = (float)yEnd,
                Color = (int)color,
                IsTrace = isTrace,
                ArcTaps = evt.SubEvents.Select(x => ParseArcTap(x, timingGroup, (int)tick, (int)endTick)).ToList(),
                Sfx = hitSound,
                TimingGroup = timingGroup,
                Line = evt.LineNumber,
                ArcResolution = (float)arcResolution
            };
        }

        protected virtual RawArcTap ParseArcTap(AntlrEvent evt, int timingGroup, int parentTick, int parentEndTick)
        {
            if (evt.Name != "arctap")
            {
                throw new ChartReaderException(evt.Raw, RawEventType.ArcTap, evt, ChartError.Kind.Parsing);
            }

            var validator = new ChartReaderValidator(evt, RawEventType.ArcTap);
            validator.Require(evt.Values.Count is 1 or 2);
            validator.Require(evt.Values[0].TryGetAlgebraicValue(out var tick));

            if (tick < parentTick || tick > parentEndTick)
            {
                throw new ChartReaderException(evt.Raw, RawEventType.ArcTap, evt, ChartError.Kind.ArcTapOutOfRange);
            }

            double width = 1;
            if (evt.Values.Count >= 2) evt.Values[1].TryGetAlgebraicValue(out width);

            return new RawArcTap
            {
                Type = RawEventType.ArcTap,
                Timing = (int)tick,
                TimingGroup = timingGroup,
                Width = (float)width,
                Line = evt.LineNumber,
                CharacterStart = evt.ColumnNumber,
                Length = evt.Raw.Length
            };
        }

        protected virtual RawSceneControl ParseSceneControl(AntlrEvent evt, int timingGroup)
        {
            const string trackDisplay = "trackdisplay";

            var validator = new ChartReaderValidator(evt, RawEventType.SceneControl);

            validator.Require(evt.Values.Count >= 2);
            validator.Require(evt.Values[0].TryGetAlgebraicValue(out var tick));
            validator.Require(evt.Values[1].TryGetStringValue(out var type));

            // parameter-less
            if (evt.Values.Count == 2)
                return type.ToLower() switch
                {
                    // https://github.com/freeze-dolphin/aff-compose/blob/17d0948c3f3726336661df4b68b0e5e2a86e3ef6/src/commonMain/kotlin/com/tairitsu/compose/filter/ShimFilter.kt#L29
                    "trackhide" => new RawSceneControl
                    {
                        Timing = (int)tick,
                        Type = RawEventType.SceneControl,
                        Arguments = new List<object>
                        {
                            1000,
                            0
                        },
                        SceneControlTypeName = trackDisplay,
                        TimingGroup = timingGroup,
                        Line = evt.LineNumber
                    },

                    // https://github.com/freeze-dolphin/aff-compose/blob/17d0948c3f3726336661df4b68b0e5e2a86e3ef6/src/commonMain/kotlin/com/tairitsu/compose/filter/ShimFilter.kt#L30
                    "trackshow" => new RawSceneControl
                    {
                        Timing = (int)tick,
                        Type = RawEventType.SceneControl,
                        Arguments = new List<object>
                        {
                            1000,
                            255
                        },
                        SceneControlTypeName = trackDisplay,
                        TimingGroup = timingGroup,
                        Line = evt.LineNumber
                    },

                    _ => new RawSceneControl
                    {
                        Timing = (int)tick,
                        Type = RawEventType.SceneControl,
                        Arguments = new List<object>(),
                        SceneControlTypeName = type,
                        TimingGroup = timingGroup,
                        Line = evt.LineNumber
                    }
                };

            // cast types
            var param = evt.Values.GetRange(2, evt.Values.Count - 2);
            validator.Require(param.All(x => x.Type is AntlrValueType.String or AntlrValueType.Algebraic));

            var typedParam = param.Select(x =>
            {
                return x.Type switch
                {
                    AntlrValueType.String => x.GetStringValue(),
                    AntlrValueType.Algebraic => (object)x.GetAlgebraicValue(),
                    _ => ChartError.Property(x.Raw,
                        evt.LineNumber,
                        RawEventType.SceneControl,
                        0,
                        evt.Raw.Length,
                        ChartError.Kind.Parsing)
                };
            }).ToList();

            return type.ToLower() switch
            {
                // https://github.com/freeze-dolphin/aff-compose/blob/17d0948c3f3726336661df4b68b0e5e2a86e3ef6/src/commonMain/kotlin/com/tairitsu/compose/filter/ShimFilter.kt#L32-L36
                trackDisplay => new RawSceneControl
                {
                    Timing = (int)tick,
                    Type = RawEventType.SceneControl,
                    Arguments = new List<object>
                    {
                        Mathf.RoundToInt((float)typedParam[0] * 1000),
                        typedParam[1]
                    },
                    SceneControlTypeName = trackDisplay,
                    TimingGroup = timingGroup,
                    Line = evt.LineNumber
                },

                _ => new RawSceneControl
                {
                    Timing = (int)tick,
                    Type = RawEventType.SceneControl,
                    Arguments = typedParam,
                    SceneControlTypeName = type,
                    TimingGroup = timingGroup,
                    Line = evt.LineNumber
                }
            };
        }

        protected virtual RawCamera ParseCamera(AntlrEvent evt, int timingGroup)
        {
            var validator = new ChartReaderValidator(evt, RawEventType.Camera);

            validator.Require(evt.Values.Count == 9);
            validator.Require(evt.Values[0].TryGetAlgebraicValue(out var tick));
            validator.Require(evt.Values[1].TryGetAlgebraicValue(out var mx));
            validator.Require(evt.Values[2].TryGetAlgebraicValue(out var my));
            validator.Require(evt.Values[3].TryGetAlgebraicValue(out var mz));
            validator.Require(evt.Values[4].TryGetAlgebraicValue(out var rx));
            validator.Require(evt.Values[5].TryGetAlgebraicValue(out var ry));
            validator.Require(evt.Values[6].TryGetAlgebraicValue(out var rz));
            validator.Require(evt.Values[7].TryGetStringValue(out var type));
            validator.Require(evt.Values[8].TryGetAlgebraicValue(out var duration) && duration >= 0, ChartError.Kind.DurationNegative);

            return new RawCamera
            {
                TimingGroup = timingGroup,
                Timing = (int)tick,
                Duration = (int)duration,
                Move = new Vector3((float)mx, (float)my, (float)mz),
                Rotate = new Vector3((float)rx, (float)ry, (float)rz),
                CameraType = type,
                Type = RawEventType.Camera,
                Line = evt.LineNumber
            };
        }

        protected virtual RawInclude ParseInclude(AntlrEvent evt)
        {
            var validator = new ChartReaderValidator(evt, RawEventType.Include);

            validator.Require(evt.Values.Count == 1);
            validator.Require(evt.Values[0].Type == AntlrValueType.String);

            return new RawInclude
            {
                File = evt.Values[0].GetStringValue()
            };
        }

        protected virtual RawFragment ParseFragment(AntlrEvent evt)
        {
            var validator = new ChartReaderValidator(evt, RawEventType.Fragment);

            validator.Require(evt.Values.Count == 2);
            validator.Require(evt.Values[0].Type == AntlrValueType.Algebraic);
            validator.Require(evt.Values[1].Type == AntlrValueType.String);

            return new RawFragment
            {
                Timing = (int)evt.Values[0].GetAlgebraicValue(),
                File = evt.Values[0].GetStringValue()
            };
        }

        private Result<ChartFileErrors> AddInclude(RawInclude include)
        {
            AllIncludes.Add(SwitchFileName(FullPath, include.File));

            ChartReader extReader = ChartReaderFactory.GetReader(FileAccess, FullPath, include.File);
            extReader.BlockReferences(AllIncludes, AllFragments);
            Result<ChartFileErrors> parseResult = extReader.Parse();
            if (parseResult.IsError)
            {
                return parseResult.Error;
            }

            foreach (RawTimingGroup group in extReader.TimingGroups)
            {
                group.Editable = true;
                group.File = Path.Combine(RelativeDirectory, group.File);
            }

            References.Add(extReader);
            return Result<ChartFileErrors>.Ok();
        }

        private Result<ChartFileErrors> AddFragment(RawFragment fragment, int timingGroup)
        {
            AllFragments.Add(SwitchFileName(FullPath, fragment.File));

            ChartReader extReader = ChartReaderFactory.GetReader(FileAccess, FullPath, fragment.File);
            extReader.BlockReferences(AllIncludes, AllFragments);
            Result<ChartFileErrors> parseResult = extReader.Parse();
            if (parseResult.IsError)
            {
                return parseResult.Error;
            }

            foreach (RawTimingGroup group in extReader.TimingGroups)
            {
                group.Editable = false;
                group.File = Path.Combine(RelativeDirectory, group.File);
            }

            foreach (RawEvent e in extReader.Events)
            {
                if (!(e is RawTiming && e.Timing == 0))
                {
                    e.Timing += fragment.Timing;
                }

                if (e is RawHold)
                {
                    (e as RawHold).EndTiming += fragment.Timing;
                }

                if (e is RawArc)
                {
                    (e as RawArc).EndTiming += fragment.Timing;
                }
            }

            References.Add(extReader);
            return Result<ChartFileErrors>.Ok();
        }

        private string SwitchFileName(string currentPath, string target)
        {
            string dir = Path.GetDirectoryName(currentPath);
            return Path.Combine(dir, target);
        }
    }
}