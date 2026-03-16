using System;
using System.Collections.Generic;
using System.Globalization;
using ArcCreate.Utility.Parser;
using Jace;
using Jace.Execution;
using UnityEngine;
using Random = System.Random;

namespace ArcCreate.ChartFormat
{
    static class ExpressionValueUtility
    {
        public static readonly CalculationEngine ExprStringEngine = new(CultureInfo.CurrentCulture, ExecutionMode.Interpreted);

        public static readonly Dictionary<string, double> ExprStringCalculationVariables = new()
        {
            ["t"] = Environment.TickCount
        };

        private static readonly Random Rng = new();

        static ExpressionValueUtility()
        {
            ExprStringEngine.AddFunction("randint",
                (min, max) => Rng.Next((int)min, (int)max), false);

            ExprStringEngine.AddFunction("rand",
                (min, max) => min + Rng.NextDouble() * (max - min), false);

            ExprStringEngine.AddFunction("gaussint",
                (mean, stdDev) =>
                {
                    // Box-Muller
                    double u1 = 1.0 - Rng.NextDouble();
                    double u2 = 1.0 - Rng.NextDouble();

                    double randStdNormal =
                        Math.Sqrt(-2.0 * Math.Log(u1)) *
                        Math.Sin(2.0 * Math.PI * u2);

                    return Math.Round(mean + stdDev * randStdNormal);
                }, false);

            ExprStringEngine.AddFunction("gauss",
                (mean, stdDev) =>
                {
                    // Box-Muller
                    double u1 = 1.0 - Rng.NextDouble();
                    double u2 = 1.0 - Rng.NextDouble();

                    double randStdNormal =
                        Math.Sqrt(-2.0 * Math.Log(u1)) *
                        Math.Sin(2.0 * Math.PI * u2);

                    return mean + stdDev * randStdNormal;
                }, false);
        }
    }

    public class ExpressionValue<V> : IFormattable, IEquatable<ExpressionValue<V>>, IComparable<ExpressionValue<V>>
        where V : IEquatable<V>, IComparable<V>
    {
        public readonly V Value;
        public readonly string Expr;

        private ExpressionValue(V value, string expr)
        {
            Value = value;
            Expr = expr;
        }

        public bool IsValue => Expr == null;
        public bool IsExpr => Expr != null;

        private V exprEvalCache = ConvertToV(0);
        private bool hasCachedValue = false;

        public static implicit operator ExpressionValue<V>(V value) => new(value, null);
        public static implicit operator ExpressionValue<V>(string expr) => new(default, expr);

        public static ExpressionValue<V> FromValue(V value) => value;
        public static ExpressionValue<V> FromExpr(string expr) => expr;

        public static implicit operator V(ExpressionValue<V> exprValue) => exprValue.GetValueOrEval();

        public V GetValueOrEval()
        {
            if (IsValue) return Value;
            if (hasCachedValue) return exprEvalCache;

            bool success = Evaluator.TryCalculate(Expr,
                ExpressionValueUtility.ExprStringCalculationVariables,
                out float evalResult,
                ExpressionValueUtility.ExprStringEngine);

            if (success)
            {
                hasCachedValue = true;
                exprEvalCache = ConvertToV(evalResult);
                return exprEvalCache;
            }

            throw new ArgumentException("This expression and could not be evaluated.");
        }

        public void ClearCache()
        {
            hasCachedValue = false;
            exprEvalCache = ConvertToV(0);
        }

        public bool TryGetValueOrEval(out V eval)
        {
            try
            {
                eval = GetValueOrEval();
                return true;
            }
            catch
            {
                eval = default;
                return false;
            }
        }

        private static V ConvertToV(float value)
        {
            Type type = typeof(V);

            if (type == typeof(float)) return (V)(object)value;
            if (type == typeof(int)) return (V)(object)Mathf.RoundToInt(value);
            if (type == typeof(double)) return (V)(object)(double)value;
            if (type == typeof(long)) return (V)(object)(long)Mathf.Round(value);
            if (type == typeof(short)) return (V)(object)(short)Mathf.Round(value);
            if (type == typeof(byte)) return (V)(object)(byte)Mathf.Clamp(Mathf.Round(value), 0, 255);

            return (V)Convert.ChangeType(value, type);
        }

        public override string ToString()
        {
            return IsExpr ? $"`{Expr}`" : Value.ToString();
        }

        public string ToString(string format, IFormatProvider formatProvider = null)
        {
            if (IsExpr) return $"`{Expr}`";

            if (Value is IFormattable formattable)
            {
                return formattable.ToString(format, formatProvider);
            }

            return Value.ToString();
        }

        public ExpressionValue<T> Cast<T>() where T : IEquatable<T>, IComparable<T>
        {
            if (IsValue)
            {
                switch (Value)
                {
                    case float floatValue when typeof(T) == typeof(double):
                        return new ExpressionValue<T>((T)(object)(double)floatValue, null);
                    case IConvertible:
                        try
                        {
                            T convertedValue = (T)Convert.ChangeType(Value, typeof(T));
                            return new ExpressionValue<T>(convertedValue, null);
                        }
                        catch
                        {
                            throw new InvalidCastException($"Cannot cast {typeof(V)} to {typeof(T)}");
                        }
                    default:
                        throw new InvalidCastException($"Cannot cast {typeof(V)} to {typeof(T)}");
                }
            }

            return new ExpressionValue<T>(default, Expr);
        }

        public bool Equals(ExpressionValue<V> other)
        {
            return EqualityComparer<V>.Default.Equals(Value, other.Value) && Expr == other.Expr;
        }

        public int CompareTo(ExpressionValue<V> other) => GetValueOrEval().CompareTo(other.GetValueOrEval());

        public override bool Equals(object obj)
        {
            return obj is ExpressionValue<V> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value, Expr);
        }
    }
}