using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;

using ExtendedNumerics;

using NCalc.Exceptions;
using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain
{
    public struct Vector :
        IEquatable<Vector>,
        IFormattable
    {
        public static double Tolerance { get; set; } = 1e-10;

        private readonly BigDecimal[] _components;

        private readonly MathHelperOptions _mathHelperOptions;

        public int Dimensions => _components?.Length ?? 0;

        public BigDecimal this[int index] => _components[index];

        public IReadOnlyList<BigDecimal> Components => _components;

        public Vector(BigDecimal[] components, MathHelperOptions options = default)
        {
            if (components is null || components.Length == 0)
                throw new ArgumentException("Vector must have at least one dimension.", nameof(components));
            _components = components;
            _mathHelperOptions = options;
        }

        public Vector(IEnumerable<BigDecimal> components, MathHelperOptions options = default)
        {
            var arr = components?.ToArray() ?? throw new ArgumentNullException(nameof(components));
            if (arr.Length == 0)
                throw new ArgumentException("Vector must have at least one dimension.", nameof(components));
            _components = arr;
            _mathHelperOptions = options;
        }

        public Vector(IEnumerable<object> components, MathHelperOptions options = default)
        {
            var arr = components?.Select(MathHelper.ConvertToBigDecimal).ToArray()
                ?? throw new ArgumentNullException(nameof(components));
            if (arr.Length == 0)
                throw new ArgumentException("Vector must have at least one dimension.", nameof(components));
            _components = arr;
            _mathHelperOptions = options;
        }

        // Static factory methods
        public static Vector Zero(int dimensions, MathHelperOptions options = default)
        {
            if (dimensions < 1)
                throw new ArgumentOutOfRangeException(nameof(dimensions), "Dimensions must be at least 1.");
            return new Vector(new BigDecimal[dimensions], options);
        }

        public static Vector Unit(int dimensions, int axis, MathHelperOptions options = default)
        {
            if (dimensions < 1)
                throw new ArgumentOutOfRangeException(nameof(dimensions), "Dimensions must be at least 1.");
            if (axis < 0 || axis >= dimensions)
                throw new ArgumentOutOfRangeException(nameof(axis), "Axis index is out of range.");
            var components = new BigDecimal[dimensions];
            components[axis] = BigDecimal.One;
            return new Vector(components, options);
        }

        // Properties

        public bool IsZero
        {
            get
            {
                if (_components is null) return true;
                return _components.All(c => c.IsZero());
            }
        }

        /// <summary>Euclidean length (magnitude) of the vector.</summary>
        public BigDecimal Magnitude
        {
            get
            {
                BigDecimal sumOfSquares = _components.Aggregate(BigDecimal.Zero, (sum, c) => sum + c * c);
                return (BigDecimal)MathHelper.Sqrt(sumOfSquares, _mathHelperOptions)!;
            }
        }

        /// <summary>Square of the Euclidean magnitude — cheaper than Magnitude (no square root).</summary>
        public BigDecimal MagnitudeSquared =>
            _components.Aggregate(BigDecimal.Zero, (sum, c) => sum + c * c);

        // Dimension guard
        private static void CheckDimensions(Vector a, Vector b, string operation = "this operation")
        {
            if (a.Dimensions != b.Dimensions)
                throw new NCalcEvaluationException(
                    $"Vectors must have the same number of dimensions for {operation} (got {a.Dimensions} and {b.Dimensions}).");
        }

        // ── Arithmetic operators ─────────────────────────────────────────────────

        public static Vector operator +(Vector a, Vector b)
        {
            CheckDimensions(a, b, "addition");
            var result = new BigDecimal[a.Dimensions];
            for (int i = 0; i < a.Dimensions; i++)
                result[i] = a[i] + b[i];
            return new Vector(result, a._mathHelperOptions);
        }

        public static Vector operator -(Vector a, Vector b)
        {
            CheckDimensions(a, b, "subtraction");
            var result = new BigDecimal[a.Dimensions];
            for (int i = 0; i < a.Dimensions; i++)
                result[i] = a[i] - b[i];
            return new Vector(result, a._mathHelperOptions);
        }

        public static Vector operator -(Vector a)
        {
            var result = new BigDecimal[a.Dimensions];
            for (int i = 0; i < a.Dimensions; i++)
                result[i] = -a[i];
            return new Vector(result, a._mathHelperOptions);
        }

        public static Vector operator *(Vector v, BigDecimal scalar)
        {
            var result = new BigDecimal[v.Dimensions];
            for (int i = 0; i < v.Dimensions; i++)
                result[i] = v[i] * scalar;
            return new Vector(result, v._mathHelperOptions);
        }

        public static Vector operator *(BigDecimal scalar, Vector v) => v * scalar;

        public static Vector operator *(Vector v, double scalar)
        {
            BigDecimal s = new BigDecimal(scalar);
            var result = new BigDecimal[v.Dimensions];
            for (int i = 0; i < v.Dimensions; i++)
                result[i] = v[i] * s;
            return new Vector(result, v._mathHelperOptions);
        }

        public static Vector operator *(double scalar, Vector v) => v * scalar;

        public static Vector operator /(Vector v, BigDecimal scalar)
        {
            if (scalar.IsZero())
                throw new DivideByZeroException("Cannot divide a vector by zero.");
            var result = new BigDecimal[v.Dimensions];
            for (int i = 0; i < v.Dimensions; i++)
                result[i] = v[i] / scalar;
            return new Vector(result, v._mathHelperOptions);
        }

        public static Vector operator /(Vector v, double scalar)
        {
            if (scalar == 0.0)
                throw new DivideByZeroException("Cannot divide a vector by zero.");
            BigDecimal s = new BigDecimal(scalar);
            var result = new BigDecimal[v.Dimensions];
            for (int i = 0; i < v.Dimensions; i++)
                result[i] = v[i] / s;
            return new Vector(result, v._mathHelperOptions);
        }

        // ── Common vector operations ─────────────────────────────────────────────

        /// <summary>Dot (inner) product of two vectors.</summary>
        public static BigDecimal Dot(Vector a, Vector b)
        {
            CheckDimensions(a, b, "the dot product");
            BigDecimal result = BigDecimal.Zero;
            for (int i = 0; i < a.Dimensions; i++)
                result += a[i] * b[i];
            return result;
        }

        /// <summary>Cross product — defined only for 3-dimensional vectors.</summary>
        public static Vector Cross(Vector a, Vector b)
        {
            if (a.Dimensions != 3 || b.Dimensions != 3)
                throw new NCalcEvaluationException("Cross product is only defined for 3-dimensional vectors.");
            return new Vector(
                new[]
                {
                    a[1] * b[2] - a[2] * b[1],
                    a[2] * b[0] - a[0] * b[2],
                    a[0] * b[1] - a[1] * b[0]
                },
                a._mathHelperOptions);
        }

        /// <summary>Returns a unit vector in the same direction as this vector.</summary>
        public Vector Normalize()
        {
            BigDecimal mag = Magnitude;
            if (mag.IsZero())
                throw new NCalcEvaluationException("Cannot normalize a zero vector.");
            return this / mag;
        }

        /// <summary>Angle (in radians) between two vectors.</summary>
        public static BigDecimal AngleBetween(Vector a, Vector b, MathHelperOptions options)
        {
            CheckDimensions(a, b, "computing the angle");
            BigDecimal magA = a.Magnitude;
            BigDecimal magB = b.Magnitude;
            if (magA.IsZero() || magB.IsZero())
                throw new NCalcEvaluationException("Cannot compute the angle with a zero vector.");

            BigDecimal cosTheta = Dot(a, b) / (magA * magB);

            // Clamp to [-1, 1] to guard against floating-point drift
            if (cosTheta > BigDecimal.One) cosTheta = BigDecimal.One;
            if (cosTheta < -BigDecimal.One) cosTheta = -BigDecimal.One;

            return (BigDecimal)MathHelper.Acos(cosTheta, options)!;
        }

        /// <summary>Vector projection of <paramref name="a"/> onto <paramref name="b"/>.</summary>
        public static Vector Project(Vector a, Vector b)
        {
            CheckDimensions(a, b, "projection");
            BigDecimal magBSquared = b.MagnitudeSquared;
            if (magBSquared.IsZero())
                throw new NCalcEvaluationException("Cannot project onto a zero vector.");
            return b * (Dot(a, b) / magBSquared);
        }

        /// <summary>Component of <paramref name="a"/> perpendicular to <paramref name="b"/> (vector rejection).</summary>
        public static Vector Reject(Vector a, Vector b) => a - Project(a, b);

        /// <summary>Euclidean distance between two vectors (treated as points).</summary>
        public static BigDecimal Distance(Vector a, Vector b) => (a - b).Magnitude;

        /// <summary>Squared Euclidean distance — cheaper than Distance (no square root).</summary>
        public static BigDecimal DistanceSquared(Vector a, Vector b) => (a - b).MagnitudeSquared;

        /// <summary>Linear interpolation: (1-t)*a + t*b.</summary>
        public static Vector Lerp(Vector a, Vector b, BigDecimal t)
        {
            CheckDimensions(a, b, "linear interpolation");
            return a + (b - a) * t;
        }

        /// <summary>Element-wise minimum.</summary>
        public static Vector Min(Vector a, Vector b)
        {
            CheckDimensions(a, b, "Min");
            var result = new BigDecimal[a.Dimensions];
            for (int i = 0; i < a.Dimensions; i++)
                result[i] = a[i] < b[i] ? a[i] : b[i];
            return new Vector(result, a._mathHelperOptions);
        }

        /// <summary>Element-wise maximum.</summary>
        public static Vector Max(Vector a, Vector b)
        {
            CheckDimensions(a, b, "Max");
            var result = new BigDecimal[a.Dimensions];
            for (int i = 0; i < a.Dimensions; i++)
                result[i] = a[i] > b[i] ? a[i] : b[i];
            return new Vector(result, a._mathHelperOptions);
        }

        /// <summary>Element-wise absolute value.</summary>
        public static Vector Abs(Vector v)
        {
            var result = new BigDecimal[v.Dimensions];
            for (int i = 0; i < v.Dimensions; i++)
                result[i] = MathHelper.ConvertToBigDecimal(MathHelper.Abs(v[i], v._mathHelperOptions));
            return new Vector(result, v._mathHelperOptions);
        }

        /// <summary>Element-wise floor.</summary>
        public static Vector Floor(Vector v)
        {
            var result = new BigDecimal[v.Dimensions];
            for (int i = 0; i < v.Dimensions; i++)
                result[i] = MathHelper.ConvertToBigDecimal(MathHelper.Floor(v[i], v._mathHelperOptions));
            return new Vector(result, v._mathHelperOptions);
        }

        /// <summary>Element-wise ceiling.</summary>
        public static Vector Ceiling(Vector v)
        {
            var result = new BigDecimal[v.Dimensions];
            for (int i = 0; i < v.Dimensions; i++)
                result[i] = MathHelper.ConvertToBigDecimal(MathHelper.Ceiling(v[i], v._mathHelperOptions));
            return new Vector(result, v._mathHelperOptions);
        }

        /// <summary>Element-wise rounding (away from zero at midpoint).</summary>
        public static Vector Round(Vector v)
        {
            var result = new BigDecimal[v.Dimensions];
            for (int i = 0; i < v.Dimensions; i++)
                result[i] = MathHelper.ConvertToBigDecimal(MathHelper.Round(v[i], 0, MidpointRounding.AwayFromZero, v._mathHelperOptions));
            return new Vector(result, v._mathHelperOptions);
        }

        /// <summary>Returns the vector with the largest magnitude.</summary>
        public static Vector MaxByMagnitude(Vector a, Vector b) =>
            a.Magnitude >= b.Magnitude ? a : b;

        /// <summary>Returns the vector with the smallest magnitude.</summary>
        public static Vector MinByMagnitude(Vector a, Vector b) =>
            a.Magnitude <= b.Magnitude ? a : b;

        // ── System.Numerics conversions ──────────────────────────────────────────
        //
        // Fixed-size types (Vector2/3/4) use float precision and require that the
        // NCalc Vector has exactly the matching number of dimensions.
        //
        // The generic SIMD type (Vector<T>) uses whatever hardware-width the
        // current process was compiled for (Vector<T>.Count elements) and again
        // requires the dimensions to match exactly.  Conversion goes through
        // double for all numeric element types, so values are accurate to double
        // precision regardless of the element type T.
        //
        // All conversion operators are explicit in both directions to reflect
        // (a) the runtime dimension requirement and (b) the precision change
        // (BigDecimal → float/double is a narrowing conversion).

        // ── Vector2 (2-D, float) ─────────────────────────────────────────────────

        /// <summary>
        /// Converts this vector to a <see cref="Vector2"/>.
        /// Requires exactly 2 dimensions; components are narrowed to <c>float</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The vector does not have exactly 2 dimensions.</exception>
        public Vector2 ToVector2()
        {
            if (Dimensions != 2)
                throw new InvalidOperationException(
                    $"Cannot convert a {Dimensions}-dimensional vector to Vector2 — exactly 2 dimensions are required.");
            return new Vector2((float)(double)_components[0], (float)(double)_components[1]);
        }

        /// <summary>Creates an NCalc <see cref="Vector"/> from a <see cref="Vector2"/>.</summary>
        public static Vector FromVector2(Vector2 v, MathHelperOptions options = default) =>
            new Vector(new BigDecimal[] { new BigDecimal((double)v.X), new BigDecimal((double)v.Y) }, options);

        /// <summary>Explicitly converts an NCalc <see cref="Vector"/> to a <see cref="Vector2"/>.</summary>
        public static explicit operator Vector2(Vector v) => v.ToVector2();

        /// <summary>Explicitly converts a <see cref="Vector2"/> to an NCalc <see cref="Vector"/>.</summary>
        public static explicit operator Vector(Vector2 v) => FromVector2(v);

        // ── Vector3 (3-D, float) ─────────────────────────────────────────────────

        /// <summary>
        /// Converts this vector to a <see cref="Vector3"/>.
        /// Requires exactly 3 dimensions; components are narrowed to <c>float</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The vector does not have exactly 3 dimensions.</exception>
        public Vector3 ToVector3()
        {
            if (Dimensions != 3)
                throw new InvalidOperationException(
                    $"Cannot convert a {Dimensions}-dimensional vector to Vector3 — exactly 3 dimensions are required.");
            return new Vector3(
                (float)(double)_components[0],
                (float)(double)_components[1],
                (float)(double)_components[2]);
        }

        /// <summary>Creates an NCalc <see cref="Vector"/> from a <see cref="Vector3"/>.</summary>
        public static Vector FromVector3(Vector3 v, MathHelperOptions options = default) =>
            new Vector(new BigDecimal[]
            {
                new BigDecimal((double)v.X),
                new BigDecimal((double)v.Y),
                new BigDecimal((double)v.Z)
            }, options);

        /// <summary>Explicitly converts an NCalc <see cref="Vector"/> to a <see cref="Vector3"/>.</summary>
        public static explicit operator Vector3(Vector v) => v.ToVector3();

        /// <summary>Explicitly converts a <see cref="Vector3"/> to an NCalc <see cref="Vector"/>.</summary>
        public static explicit operator Vector(Vector3 v) => FromVector3(v);

        // ── Vector4 (4-D, float) ─────────────────────────────────────────────────

        /// <summary>
        /// Converts this vector to a <see cref="Vector4"/>.
        /// Requires exactly 4 dimensions; components are narrowed to <c>float</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The vector does not have exactly 4 dimensions.</exception>
        public Vector4 ToVector4()
        {
            if (Dimensions != 4)
                throw new InvalidOperationException(
                    $"Cannot convert a {Dimensions}-dimensional vector to Vector4 — exactly 4 dimensions are required.");
            return new Vector4(
                (float)(double)_components[0],
                (float)(double)_components[1],
                (float)(double)_components[2],
                (float)(double)_components[3]);
        }

        /// <summary>Creates an NCalc <see cref="Vector"/> from a <see cref="Vector4"/>.</summary>
        public static Vector FromVector4(Vector4 v, MathHelperOptions options = default) =>
            new Vector(new BigDecimal[]
            {
                new BigDecimal((double)v.X),
                new BigDecimal((double)v.Y),
                new BigDecimal((double)v.Z),
                new BigDecimal((double)v.W)
            }, options);

        /// <summary>Explicitly converts an NCalc <see cref="Vector"/> to a <see cref="Vector4"/>.</summary>
        public static explicit operator Vector4(Vector v) => v.ToVector4();

        /// <summary>Explicitly converts a <see cref="Vector4"/> to an NCalc <see cref="Vector"/>.</summary>
        public static explicit operator Vector(Vector4 v) => FromVector4(v);

        // ── Vector<T> (hardware-width SIMD) ──────────────────────────────────────

        /// <summary>
        /// Converts this vector to a <see cref="Vector{T}"/>.
        /// The number of dimensions must equal <see cref="Vector{T}.Count"/> on the
        /// current hardware; otherwise an <see cref="InvalidOperationException"/> is thrown.
        /// Component values are narrowed from <see cref="BigDecimal"/> to <typeparamref name="T"/>
        /// via <c>double</c>, so values are accurate to double precision.
        /// </summary>
        /// <typeparam name="T">
        /// A numeric primitive supported by <see cref="Vector{T}"/>
        /// (e.g. <c>float</c>, <c>double</c>, <c>int</c>, <c>long</c>).
        /// </typeparam>
        /// <exception cref="InvalidOperationException">
        /// The vector's dimension count does not match <see cref="Vector{T}.Count"/>.
        /// </exception>
        public System.Numerics.Vector<T> ToSystemVector<T>() where T : struct
        {
            int count = System.Numerics.Vector<T>.Count;
            if (Dimensions != count)
                throw new InvalidOperationException(
                    $"Cannot convert a {Dimensions}-dimensional vector to Vector<{typeof(T).Name}> — " +
                    $"the current hardware SIMD width requires exactly {count} elements.");

            var values = new T[count];
            for (int i = 0; i < count; i++)
                values[i] = (T)Convert.ChangeType((double)_components[i], typeof(T));

            return new System.Numerics.Vector<T>(values);
        }

        /// <summary>
        /// Creates an NCalc <see cref="Vector"/> from a <see cref="Vector{T}"/>.
        /// All <see cref="Vector{T}.Count"/> elements are converted to <see cref="BigDecimal"/>
        /// via <c>double</c>.
        /// </summary>
        /// <typeparam name="T">
        /// A numeric primitive supported by <see cref="Vector{T}"/>
        /// (e.g. <c>float</c>, <c>double</c>, <c>int</c>, <c>long</c>).
        /// </typeparam>
        public static Vector FromSystemVector<T>(System.Numerics.Vector<T> v, MathHelperOptions options = default)
            where T : struct
        {
            int count = System.Numerics.Vector<T>.Count;
            var buffer = new T[count];
            v.CopyTo(buffer);

            var components = new BigDecimal[count];
            for (int i = 0; i < count; i++)
                components[i] = new BigDecimal(Convert.ToDouble(buffer[i]));

            return new Vector(components, options);
        }

        // ── Equality ─────────────────────────────────────────────────────────────

        public bool Equals(Vector other)
        {
            if (Dimensions != other.Dimensions) return false;
            for (int i = 0; i < Dimensions; i++)
                if (!_components[i].Equals(other._components[i]))
                    return false;
            return true;
        }

        public override bool Equals(object? obj) => obj is Vector other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 17;
            if (_components is not null)
                foreach (var c in _components)
                    hash = hash * 23 + c.GetHashCode();
            return hash;
        }

        public static bool operator ==(Vector a, Vector b) => a.Equals(b);
        public static bool operator !=(Vector a, Vector b) => !a.Equals(b);

        // ── Formatting ───────────────────────────────────────────────────────────

        public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            var sb = new StringBuilder("[");
            if (_components is not null)
            {
                for (int i = 0; i < _components.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(_components[i].ToString());
                }
            }
            sb.Append(']');
            return sb.ToString();
        }
    }

    /// <summary>
    /// Tolerance-based equality comparer for <see cref="Vector"/>.
    /// Two vectors are considered equal if every component pair differs by at most the tolerance.
    /// </summary>
    public sealed class VectorToleranceComparer : IEqualityComparer<Vector>
    {
        private readonly double? _tolerance;

        public VectorToleranceComparer() { _tolerance = null; }

        public VectorToleranceComparer(double tolerance) { _tolerance = tolerance; }

        public bool Equals(Vector a, Vector b)
        {
            double tol = _tolerance ?? Vector.Tolerance;
            if (a.Dimensions != b.Dimensions) return false;
            for (int i = 0; i < a.Dimensions; i++)
            {
                BigDecimal diff = a[i] - b[i];
                if ((diff.IsPositive() && diff > tol) || (diff < -tol))
                    return false;
            }
            return true;
        }

        public int GetHashCode(Vector obj)
        {
            double tol = _tolerance ?? Vector.Tolerance;
            int hash = 17;
            if (tol == 0.0)
                return obj.GetHashCode();
            for (int i = 0; i < obj.Dimensions; i++)
            {
                long bucket = (long)Math.Round((double)(obj[i] / tol));
                hash = hash * 23 + bucket.GetHashCode();
            }
            return hash;
        }
    }

    /// <summary>
    /// AST node representing a vector literal: a fixed-length list of component expressions
    /// that are each evaluated to a numeric value and combined into a <see cref="Vector"/>.
    /// </summary>
    public sealed class VectorExpression(LogicalExpressionList expressions, MathHelperOptions mathHelperOptions) : LogicalExpression
    {
        /// <summary>
        /// The ordered list of component expressions.
        /// Each expression must evaluate to a numeric value; together they define the vector's dimensions.
        /// </summary>
        public LogicalExpressionList Expressions { get; set; } = expressions;

        public MathHelperOptions MathHelperOptions { get; } = mathHelperOptions;

        public override T Accept<T>(ILogicalExpressionVisitor<T> visitor, CancellationToken cancellationToken = default)
        {
            return visitor.Visit(this, cancellationToken);
        }

        internal override T AcceptNoRecurse<T>(ILogicalExpressionNoRecurseVisitor<T> visitor, ExpressionTask<T> task, CancellationToken cancellationToken = default)
        {
            return visitor.Visit(this, task, cancellationToken);
        }
    }
}
