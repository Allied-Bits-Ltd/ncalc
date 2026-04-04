using System;
using System.CodeDom;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ExtendedNumerics;

using NCalc.Exceptions;
using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain
{
    public struct ComplexNumber :
        IEquatable<ComplexNumber>,
        IFormattable
    {
        public static double Tolerance { get; set; } = 1e-10;

        public BigDecimal Real { get; }

        public BigDecimal Imaginary { get; }

        public bool IsReal => Imaginary.IsZero() || this.CheckIsReal();

        public bool IsImaginary => Real.IsZero() && !Imaginary.IsZero();

        public bool IsZero
        {
            get
            {
                bool realZero = MathHelper.ConvertToBigDecimal(MathHelper.Abs(Real, _mathHelperOptions)) < Tolerance;
                bool imaginaryZero = MathHelper.ConvertToBigDecimal(MathHelper.Abs(Imaginary, _mathHelperOptions)) < Tolerance;
                return realZero && imaginaryZero;
            }
        }

        private readonly MathHelperOptions _mathHelperOptions;

        private BigDecimal? _magnitude;

        public ComplexNumber(BigDecimal real)
        {
            Real = real;
            Imaginary = 0;
            _mathHelperOptions = MathHelperOptions.Empty;
        }

        public static implicit operator ComplexNumber(double value)
        {
            return new ComplexNumber(value, 0.0);
        }

        public ComplexNumber(BigDecimal real, MathHelperOptions options)
        {
            Real = real;
            Imaginary = 0;
            _mathHelperOptions = options;
        }

        public ComplexNumber(BigDecimal real, BigDecimal imaginary)
        {
            Real = real;
            Imaginary = imaginary;
            _mathHelperOptions = MathHelperOptions.Empty;
        }

        public ComplexNumber(BigDecimal real, BigDecimal imaginary, MathHelperOptions options)
        {
            Real = real;
            Imaginary = imaginary;
            _mathHelperOptions = options;
        }

        public ComplexNumber(object real, object imaginary, MathHelperOptions options)
        {
            Real = MathHelper.ConvertToBigDecimal(real);
            Imaginary = MathHelper.ConvertToBigDecimal(imaginary);
            _mathHelperOptions = options;
        }

        // Static instances
        public static readonly ComplexNumber Zero = new(BigDecimal.Zero, BigDecimal.Zero);
        public static readonly ComplexNumber One = new(BigDecimal.One, BigDecimal.Zero);
        public static readonly ComplexNumber ImaginaryOne = new(BigDecimal.Zero, BigDecimal.One);

        public BigDecimal Abs => Magnitude;

        // Magnitude (modulus)
        public BigDecimal Magnitude
        {
            get
            {
                if (_magnitude is null)
                    _magnitude = MathHelper.Hypot(Real, Imaginary, _mathHelperOptions);

                return _magnitude.Value;
            }
        }

        public BigDecimal MagnitudeSquared
        {
            get
            {
                BigDecimal realSquared = Real * Real;
                BigDecimal imaginarySquared = Imaginary * Imaginary;
                return realSquared + imaginarySquared;
            }
        }

        // Phase (argument)
        public BigDecimal Phase
        {
            get
            {
                return MathHelper.ConvertToBigDecimal(MathHelper.Atan2(Imaginary, Real, _mathHelperOptions));
            }
        }

        // Conjugate
        public ComplexNumber Conjugate() => new(Real, -Imaginary);

        public ComplexNumber Normalize()
        {
            BigDecimal magnitude = Magnitude;

            if (magnitude.IsZero())
                return Zero;

            return this / magnitude;
        }

        // Arithmetic operators
        public static ComplexNumber operator +(ComplexNumber a, ComplexNumber b) =>
            new(a.Real + b.Real, a.Imaginary + b.Imaginary);

        public static ComplexNumber operator -(ComplexNumber a, ComplexNumber b) =>
            new(a.Real - b.Real, a.Imaginary - b.Imaginary);

        public static ComplexNumber operator *(ComplexNumber a, ComplexNumber b) =>
            new(
                a.Real * b.Real - a.Imaginary * b.Imaginary,
                a.Real * b.Imaginary + a.Imaginary * b.Real
            );

        public static ComplexNumber operator /(ComplexNumber a, ComplexNumber b)
        {
            BigDecimal denom = b.Real * b.Real + b.Imaginary * b.Imaginary;

            if (denom.IsZero())
                throw new DivideByZeroException();

            return new ComplexNumber(
                (a.Real * b.Real + a.Imaginary * b.Imaginary) / denom,
                (a.Imaginary * b.Real - a.Real * b.Imaginary) / denom
            );
        }

        public static ComplexNumber operator /(ComplexNumber value, double divisor)
        {
            if (divisor == 0.0)
                throw new DivideByZeroException();

            BigDecimal real = value.Real / divisor;
            BigDecimal imaginary = value.Imaginary / divisor;

            return new ComplexNumber(real, imaginary);
        }

        public static ComplexNumber operator /(ComplexNumber value, BigDecimal divisor)
        {
            if (divisor == 0.0)
                throw new DivideByZeroException();

            BigDecimal real = value.Real / divisor;
            BigDecimal imaginary = value.Imaginary / divisor;

            return new ComplexNumber(real, imaginary);
        }

        public static ComplexNumber operator /(double scalar, ComplexNumber value)
        {
            return new ComplexNumber(scalar, 0.0) / value;
        }

        public static ComplexNumber operator /(BigDecimal scalar, ComplexNumber value)
        {
            return new ComplexNumber(scalar, 0.0) / value;
        }

        public static ComplexNumber operator +(ComplexNumber z, double r)
        {
            return new ComplexNumber(z.Real + r, z.Imaginary);
        }

        public static ComplexNumber operator +(double r, ComplexNumber z)
        {
            return new ComplexNumber(r + z.Real, z.Imaginary);
        }

        public static ComplexNumber operator -(ComplexNumber z, double r)
        {
            return new ComplexNumber(z.Real - r, z.Imaginary);
        }

        public static ComplexNumber operator -(double r, ComplexNumber z)
        {
            return new ComplexNumber(r - z.Real, -z.Imaginary);
        }

        public static ComplexNumber operator *(ComplexNumber z, double r)
        {
            return new ComplexNumber(z.Real * r, z.Imaginary * r);
        }

        public static ComplexNumber operator *(double r, ComplexNumber z)
        {
            return new ComplexNumber(r * z.Real, r * z.Imaginary);
        }

        public static ComplexNumber operator +(ComplexNumber z, BigDecimal r)
        {
            return new ComplexNumber(z.Real + r, z.Imaginary);
        }

        public static ComplexNumber operator +(BigDecimal r, ComplexNumber z)
        {
            return new ComplexNumber(r + z.Real, z.Imaginary);
        }

        public static ComplexNumber operator -(ComplexNumber z, BigDecimal r)
        {
            return new ComplexNumber(z.Real - r, z.Imaginary);
        }

        public static ComplexNumber operator -(BigDecimal r, ComplexNumber z)
        {
            return new ComplexNumber(r - z.Real, -z.Imaginary);
        }

        public static ComplexNumber operator *(ComplexNumber z, BigDecimal r)
        {
            return new ComplexNumber(z.Real * r, z.Imaginary * r);
        }

        public static ComplexNumber operator *(BigDecimal r, ComplexNumber z)
        {
            return new ComplexNumber(r * z.Real, r * z.Imaginary);
        }

        // Unary operators
        public static ComplexNumber operator -(ComplexNumber a) =>
            new(-a.Real, -a.Imaginary);

        // Equality (with tolerance)

        public bool CheckIsReal()
        {
            if (Imaginary.IsZero())
                return true;

            if ((Imaginary.IsPositive() && Imaginary > Tolerance) || (Imaginary < -Tolerance))
                return false;

            return true;
        }

        public bool Equals(ComplexNumber other)
        {
            return Real.Equals(other.Real) && Imaginary.Equals(other.Imaginary);
        }

        public override bool Equals(object? obj)
        {
            return obj is ComplexNumber other && Equals(other);
        }

        public override int GetHashCode() => (Real, Imaginary).GetHashCode();

        public static bool operator ==(ComplexNumber a, ComplexNumber b) => a.Equals(b);

        public static bool operator !=(ComplexNumber a, ComplexNumber b) => !a.Equals(b);

        // Formatting
        public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            formatProvider ??= CultureInfo.InvariantCulture;

            string realStr = Real.ToString();

            object absImg = MathHelper.Abs(Imaginary, _mathHelperOptions);

            string imagStr;

            if (absImg is BigDecimal bd)
                imagStr = bd.ToString();
            else
                if (absImg is double d)
                    imagStr = d.ToString(format, formatProvider);
                else
                    imagStr = string.Empty;

            string sign = Imaginary >= 0 ? "+" : "-";

            return $"{realStr} {sign} {imagStr}i";
        }

        // Various functions

        public ComplexNumber Reciprocal()
        {
            return One / this;
        }

        public static ComplexNumber MaxByMagnitude(ComplexNumber x, ComplexNumber y)
        {
            return x.Magnitude >= y.Magnitude ? x : y;
        }

        public static ComplexNumber MinByMagnitude(ComplexNumber x, ComplexNumber y)
        {
            return x.Magnitude <= y.Magnitude ? x : y;
        }

        public static ComplexNumber Floor(ComplexNumber value)
        {
            return new ComplexNumber(
                MathHelper.ConvertToBigDecimal(MathHelper.Floor(value.Real, MathHelperOptions.Empty)),
                MathHelper.ConvertToBigDecimal(MathHelper.Floor(value.Imaginary, MathHelperOptions.Empty)));
        }

        public static ComplexNumber Ceiling(ComplexNumber value)
        {
            return new ComplexNumber(
                MathHelper.ConvertToBigDecimal(MathHelper.Ceiling(value.Real, MathHelperOptions.Empty)),
                MathHelper.ConvertToBigDecimal(MathHelper.Ceiling(value.Imaginary, MathHelperOptions.Empty)));
        }

        public static ComplexNumber Round(ComplexNumber value)
        {
            return new ComplexNumber(
                MathHelper.ConvertToBigDecimal(MathHelper.Round(value.Real, 0, MidpointRounding.AwayFromZero, MathHelperOptions.Empty)),
                MathHelper.ConvertToBigDecimal(MathHelper.Round(value.Imaginary, 0, MidpointRounding.AwayFromZero, MathHelperOptions.Empty)));
        }

        public static ComplexNumber Truncate(ComplexNumber value)
        {
            return new ComplexNumber(
                MathHelper.ConvertToBigDecimal(MathHelper.Truncate(value.Real, MathHelperOptions.Empty)),
                MathHelper.ConvertToBigDecimal(MathHelper.Truncate(value.Imaginary, MathHelperOptions.Empty)));
        }

        public static ComplexNumber FromPolar(BigDecimal magnitude, BigDecimal phase, MathHelperOptions options)
        {
            BigDecimal real = magnitude * MathHelper.ConvertToBigDecimal(MathHelper.Cos(phase, options)!);
            BigDecimal imaginary = magnitude * MathHelper.ConvertToBigDecimal(MathHelper.Sin(phase, options)!);
            return new ComplexNumber(real, imaginary);
        }

        public static ComplexNumber Sqrt(ComplexNumber value, MathHelperOptions options)
        {
            if (value.Real.IsZero() && value.Imaginary.IsZero())
                return Zero;

            BigDecimal magnitude = value.Magnitude;
            BigDecimal realPart = MathHelper.ConvertToBigDecimal(MathHelper.Sqrt((magnitude + value.Real) / 2.0, options)!);
            BigDecimal imaginaryPart = MathHelper.ConvertToBigDecimal(MathHelper.Sqrt((magnitude - value.Real) / 2.0, options)!);

            if (value.Imaginary < 0.0)
                imaginaryPart = -imaginaryPart;

            return new ComplexNumber(realPart, imaginaryPart);
        }

        public static ComplexNumber Exp(ComplexNumber value, MathHelperOptions options)
        {
            BigDecimal expReal = MathHelper.ConvertToBigDecimal(MathHelper.Exp(value.Real, options)!);
            BigDecimal cosImaginary = MathHelper.ConvertToBigDecimal(MathHelper.Cos(value.Imaginary, options)!);
            BigDecimal sinImaginary = MathHelper.ConvertToBigDecimal(MathHelper.Sin(value.Imaginary, options)!);

            BigDecimal real = expReal * cosImaginary;
            BigDecimal imaginary = expReal * sinImaginary;

            return new ComplexNumber(real, imaginary);
        }

        public static ComplexNumber Log(ComplexNumber value, MathHelperOptions options)
        {
            if (value.Real == 0.0 && value.Imaginary == 0.0)
                throw new ArgumentOutOfRangeException(nameof(value), "Logarithm of zero is undefined.");

            BigDecimal magnitude = value.Magnitude;
            BigDecimal phase = value.Phase;

            BigDecimal real = MathHelper.ConvertToBigDecimal(MathHelper.Ln(magnitude, options)!);
            BigDecimal imaginary = phase;

            return new ComplexNumber(real, imaginary);
        }

        public static ComplexNumber Log(ComplexNumber value, BigDecimal baseValue, MathHelperOptions options)
        {
            if (baseValue.IsNegative() || baseValue.IsZero() || baseValue == 1.0)
                throw new ArgumentOutOfRangeException(nameof(baseValue), "Logarithm base must be positive and not equal to 1.");

            ComplexNumber naturalLog = Log(value, options);
            BigDecimal naturalLogOfBase = MathHelper.ConvertToBigDecimal(MathHelper.Ln(baseValue, options)!);

            ComplexNumber result = naturalLog / naturalLogOfBase;
            return result;
        }

        /// <summary>Returns the base-10 logarithm of a complex number.</summary>
        public static ComplexNumber Log10(ComplexNumber value, MathHelperOptions options) =>
            Log(value, new BigDecimal(10), options);

        public static ComplexNumber Pow(ComplexNumber value, ComplexNumber power, MathHelperOptions options)
        {
            if (value.Real.IsZero() && value.Imaginary.IsZero())
            {
                if (power.Real.IsZero() && power.Imaginary.IsZero())
                    return One;

                return Zero;
            }

            ComplexNumber logarithm = Log(value, options);
            ComplexNumber product = power * logarithm;
            ComplexNumber result = Exp(product, options);
            return result;
        }

        public static ComplexNumber Pow(ComplexNumber value, BigDecimal power, MathHelperOptions options)
        {
            ComplexNumber complexPower = new ComplexNumber(power, 0.0);
            ComplexNumber result = Pow(value, complexPower, options);
            return result;
        }

        public static ComplexNumber Sin(ComplexNumber value, MathHelperOptions options)
        {
            BigDecimal x = value.Real;
            BigDecimal y = value.Imaginary;

            BigDecimal sinX = MathHelper.ConvertToBigDecimal(MathHelper.Sin(x, options)!);
            BigDecimal cosX = MathHelper.ConvertToBigDecimal(MathHelper.Cos(x, options)!);
            BigDecimal sinhY = MathHelper.ConvertToBigDecimal(MathHelper.Sinh(y, options)!);
            BigDecimal coshY = MathHelper.ConvertToBigDecimal(MathHelper.Cosh(y, options)!);

            BigDecimal real = sinX * coshY;
            BigDecimal imaginary = cosX * sinhY;

            return new ComplexNumber(real, imaginary);
        }

        public static ComplexNumber Cos(ComplexNumber value, MathHelperOptions options)
        {
            BigDecimal x = value.Real;
            BigDecimal y = value.Imaginary;

            BigDecimal sinX = MathHelper.ConvertToBigDecimal(MathHelper.Sin(x, options)!);
            BigDecimal cosX = MathHelper.ConvertToBigDecimal(MathHelper.Cos(x, options)!);
            BigDecimal sinhY = MathHelper.ConvertToBigDecimal(MathHelper.Sinh(y, options)!);
            BigDecimal coshY = MathHelper.ConvertToBigDecimal(MathHelper.Cosh(y, options)!);

            BigDecimal real = cosX * coshY;
            BigDecimal imaginary = -sinX * sinhY;

            return new ComplexNumber(real, imaginary);
        }

        public static ComplexNumber Tan(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber sine = Sin(value, options);
            ComplexNumber cosine = Cos(value, options);
            ComplexNumber result = sine / cosine;
            return result;
        }

        public static ComplexNumber Cot(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber tangent = Tan(value, options);
            ComplexNumber result = One / tangent;
            return result;
        }

        public static ComplexNumber Sec(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber cosine = Cos(value, options);
            ComplexNumber result = One / cosine;
            return result;
        }

        public static ComplexNumber Csc(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber sine = Sin(value, options);
            ComplexNumber result = One / sine;
            return result;
        }

        public static ComplexNumber Asin(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber iz = ImaginaryOne * value;
            ComplexNumber oneMinusZSquared = One - value * value;
            ComplexNumber squareRoot = Sqrt(oneMinusZSquared, options);
            ComplexNumber inside = iz + squareRoot;
            ComplexNumber logarithm = Log(inside, options);
            ComplexNumber result = -ImaginaryOne * logarithm;
            return result;
        }

        public static ComplexNumber Acos(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber oneMinusZSquared = One - value * value;
            ComplexNumber squareRoot = Sqrt(oneMinusZSquared, options);
            ComplexNumber inside = value + ImaginaryOne * squareRoot;
            ComplexNumber logarithm = Log(inside, options);
            ComplexNumber result = -ImaginaryOne * logarithm;
            return result;
        }

        public static ComplexNumber Atan(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber iz = ImaginaryOne * value;
            ComplexNumber oneMinusIz = One - iz;
            ComplexNumber onePlusIz = One + iz;

            ComplexNumber log1 = Log(oneMinusIz, options);
            ComplexNumber log2 = Log(onePlusIz, options);
            ComplexNumber difference = log1 - log2;

            ComplexNumber result = (ImaginaryOne / 2.0) * difference;
            return result;
        }

        public static ComplexNumber Acot(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber reciprocal = One / value;
            ComplexNumber result = Atan(reciprocal, options);
            return result;
        }

        public static ComplexNumber Asec(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber reciprocal = One / value;
            ComplexNumber result = Acos(reciprocal, options);
            return result;
        }

        public static ComplexNumber Acsc(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber reciprocal = One / value;
            ComplexNumber result = Asin(reciprocal, options);
            return result;
        }

        public static ComplexNumber Sinh(ComplexNumber value, MathHelperOptions options)
        {
            BigDecimal x = value.Real;
            BigDecimal y = value.Imaginary;

            BigDecimal sinhX = MathHelper.ConvertToBigDecimal(MathHelper.Sinh(x, options)!);
            BigDecimal coshX = MathHelper.ConvertToBigDecimal(MathHelper.Cosh(x, options)!);
            BigDecimal sinY = MathHelper.ConvertToBigDecimal(MathHelper.Sin(y, options)!);
            BigDecimal cosY = MathHelper.ConvertToBigDecimal(MathHelper.Cos(y, options)!);

            BigDecimal real = sinhX * cosY;
            BigDecimal imaginary = coshX * sinY;

            return new ComplexNumber(real, imaginary);
        }

        public static ComplexNumber Cosh(ComplexNumber value, MathHelperOptions options)
        {
            BigDecimal x = value.Real;
            BigDecimal y = value.Imaginary;

            BigDecimal sinhX = MathHelper.ConvertToBigDecimal(MathHelper.Sinh(x, options)!);
            BigDecimal coshX = MathHelper.ConvertToBigDecimal(MathHelper.Cosh(x, options)!);
            BigDecimal sinY = MathHelper.ConvertToBigDecimal(MathHelper.Sin(y, options)!);
            BigDecimal cosY = MathHelper.ConvertToBigDecimal(MathHelper.Cos(y, options)!);

            BigDecimal real = coshX * cosY;
            BigDecimal imaginary = sinhX * sinY;

            return new ComplexNumber(real, imaginary);
        }

        public static ComplexNumber Tanh(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber sinh = Sinh(value, options);
            ComplexNumber cosh = Cosh(value, options);
            ComplexNumber result = sinh / cosh;
            return result;
        }

        public static ComplexNumber Coth(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber tanh = Tanh(value, options);
            ComplexNumber result = One / tanh;
            return result;
        }

        public static ComplexNumber Sech(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber cosh = Cosh(value, options);
            ComplexNumber result = One / cosh;
            return result;
        }

        public static ComplexNumber Csch(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber sinh = Sinh(value, options);
            ComplexNumber result = One / sinh;
            return result;
        }

        public static ComplexNumber Asinh(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber zSquared = value * value;
            ComplexNumber insideSqrt = zSquared + One;
            ComplexNumber squareRoot = Sqrt(insideSqrt, options);
            ComplexNumber insideLog = value + squareRoot;
            ComplexNumber result = Log(insideLog, options);
            return result;
        }

        public static ComplexNumber Acosh(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber sqrt1 = Sqrt(value - One, options);
            ComplexNumber sqrt2 = Sqrt(value + One, options);
            ComplexNumber insideLog = value + sqrt1 * sqrt2;
            ComplexNumber result = Log(insideLog, options);
            return result;
        }

        public static ComplexNumber Atanh(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber numerator = One + value;
            ComplexNumber denominator = One - value;
            ComplexNumber quotient = numerator / denominator;
            ComplexNumber logarithm = Log(quotient, options);
            ComplexNumber result = logarithm / 2.0;
            return result;
        }

        public static ComplexNumber Acoth(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber numerator = value + One;
            ComplexNumber denominator = value - One;
            ComplexNumber quotient = numerator / denominator;
            ComplexNumber logarithm = Log(quotient, options);
            ComplexNumber result = logarithm / 2.0;
            return result;
        }

        public static ComplexNumber Asech(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber reciprocal = One / value;
            ComplexNumber result = Acosh(reciprocal, options);
            return result;
        }

        public static ComplexNumber Acsch(ComplexNumber value, MathHelperOptions options)
        {
            ComplexNumber reciprocal = One / value;
            ComplexNumber result = Asinh(reciprocal, options);
            return result;
        }

        // ── System.Numerics.Complex conversions ──────────────────────────────────
        //
        // System.Numerics.Complex stores both components as double, so conversion
        // from ComplexNumber is a narrowing operation (BigDecimal → double).
        // Both conversion operators are therefore explicit in both directions.

        /// <summary>
        /// Converts this complex number to a <see cref="Complex"/>.
        /// Both the real and imaginary components are narrowed from
        /// <see cref="BigDecimal"/> to <c>double</c>.
        /// </summary>
        public Complex ToComplex() =>
            new Complex((double)Real, (double)Imaginary);

        /// <summary>
        /// Creates a <see cref="ComplexNumber"/> from a <see cref="Complex"/>.
        /// Both components are widened from <c>double</c> to <see cref="BigDecimal"/>.
        /// </summary>
        public static ComplexNumber FromComplex(Complex c, MathHelperOptions options = default) =>
            new ComplexNumber(new BigDecimal(c.Real), new BigDecimal(c.Imaginary), options);

        /// <summary>
        /// Explicitly converts a <see cref="ComplexNumber"/> to a <see cref="Complex"/>.
        /// </summary>
        public static explicit operator Complex(ComplexNumber c) => c.ToComplex();

        /// <summary>
        /// Explicitly converts a <see cref="Complex"/> to a <see cref="ComplexNumber"/>.
        /// </summary>
        public static explicit operator ComplexNumber(Complex c) => FromComplex(c);
    }

    public sealed class ComplexNumberToleranceComparer : IEqualityComparer<ComplexNumber>
    {
        private double? _tolerance;

        public ComplexNumberToleranceComparer()
        {
            _tolerance = null;
        }

        public ComplexNumberToleranceComparer(double tolerance)
        {
            _tolerance = tolerance;
        }

        public bool Equals(ComplexNumber a, ComplexNumber b)
        {
            double tol = _tolerance ?? ComplexNumber.Tolerance;

            BigDecimal diff = a.Real - b.Real;
            if ((diff.IsPositive() && diff > tol) || (diff < -tol))
                return false;

            diff = a.Imaginary - b.Imaginary;
            if ((diff.IsPositive() && diff > tol) || (diff < -tol))
                return false;

            return true;
        }

        public int GetHashCode(ComplexNumber obj)
        {
            double tol = _tolerance ?? ComplexNumber.Tolerance;

            if (tol == 0.0)
                return obj.GetHashCode();

            long realBucket = (long)Math.Round((double)(obj.Real / tol));
            long imaginaryBucket = (long)Math.Round((double)(obj.Imaginary / tol));

            unchecked
            {
                int hash = 17;
                hash = hash * 23 + realBucket.GetHashCode();
                hash = hash * 23 + imaginaryBucket.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class ImaginaryNumberExpression : LogicalExpression
    {
        public LogicalExpression Expression { get; set; }

        private readonly MathHelperOptions _mathHelperOptions;

        public ImaginaryNumberExpression(LogicalExpression expression, MathHelperOptions mathHelperOptions)
        {
            Expression = expression;
            _mathHelperOptions = mathHelperOptions;
        }

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