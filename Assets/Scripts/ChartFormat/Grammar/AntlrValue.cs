using System;
using System.Runtime.CompilerServices;
using Antlr4.Runtime;

namespace ArcCreate.ChartFormat.Grammar
{
    public enum AntlrValueType
    {
        String,
        Algebraic,
        KeyValuePair
    }

    public class AntlrValue : IAntlrPositionTrack
    {
        public string Raw { get; }
        public AntlrValueType Type { get; }

        public int LineNumber { get; }
        public int ColumnNumber { get; }

        private string StringValue { get; }
        private double AlgebraicValue { get; }
        private Tuple<string, AntlrValue> KeyValuePair { get; }

        public bool HasStringValue { get; protected set; }
        public bool HasAlgebraicValue { get; protected set; }
        public bool HasKeyValuePair { get; protected set; }

        public static AntlrValue GetEmpty(IToken start) =>
            new("", AntlrValueType.String, start, stringValue: "");

        public static AntlrValue GetEmpty(int lineNumber, int columnNumber) =>
            new("", AntlrValueType.String, lineNumber, columnNumber, stringValue: "");

        public AntlrValue(string raw, AntlrValueType type, int lineNumber, int columnNumber,
            string stringValue = null,
            double algebraicValue = double.NaN,
            Tuple<string, AntlrValue> keyValuePair = null
        )
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            Type = type;

            LineNumber = lineNumber;
            ColumnNumber = columnNumber;

            StringValue = stringValue;
            AlgebraicValue = algebraicValue;
            KeyValuePair = keyValuePair;

            switch (type)
            {
                case AntlrValueType.String:
                    if (stringValue == null)
                        throw new ArgumentException("Invalid value", nameof(stringValue));

                    HasStringValue = true;
                    break;
                case AntlrValueType.Algebraic:
                    if (double.IsNaN(AlgebraicValue))
                        throw new ArgumentException("Invalid value", nameof(algebraicValue));

                    HasAlgebraicValue = true;
                    break;
                case AntlrValueType.KeyValuePair:
                    if (keyValuePair == null)
                        throw new ArgumentException("Invalid value", nameof(keyValuePair));

                    HasKeyValuePair = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public AntlrValue(string raw, AntlrValueType type, IToken start,
            string stringValue = null,
            double algebraicValue = double.NaN,
            Tuple<string, AntlrValue> keyValuePair = null
        ) : this(raw, type, start.Line, start.Column, stringValue, algebraicValue, keyValuePair)
        {
        }

        public AntlrValue(string raw, AntlrValueType type, IToken start, IToken parentStart,
            string stringValue = null,
            double algebraicValue = double.NaN,
            Tuple<string, AntlrValue> keyValuePair = null
        ) : this(raw, type, start.Line, start.Column + parentStart.Column, stringValue, algebraicValue, keyValuePair)
        {
        }

        public string GetStringValue() =>
            HasStringValue ? StringValue : throw new InvalidOperationException();

        public double GetAlgebraicValue() =>
            HasAlgebraicValue ? AlgebraicValue : throw new InvalidOperationException();

        public Tuple<string, AntlrValue> GetKeyValuePair() =>
            HasKeyValuePair ? KeyValuePair : throw new InvalidOperationException();

        public bool TryGetStringValue(out string stringValue)
        {
            stringValue = StringValue;
            return HasStringValue;
        }

        public bool TryGetAlgebraicValue(out double algebraicValue)
        {
            algebraicValue = AlgebraicValue;
            return HasAlgebraicValue;
        }

        public bool TryGetKeyValuePair(out Tuple<string, AntlrValue> keyValuePair)
        {
            keyValuePair = KeyValuePair;
            return HasKeyValuePair;
        }

        public override string ToString() =>
            Type switch
            {
                AntlrValueType.String => $"Value(String, \"{StringValue}\")",
                AntlrValueType.Algebraic => $"Value(Algebraic, {AlgebraicValue})",
                AntlrValueType.KeyValuePair => $"Value(KeyValuePair, {KeyValuePair?.Item1} → ...)",
                _ => "Value(unknown)"
            };
    }
}