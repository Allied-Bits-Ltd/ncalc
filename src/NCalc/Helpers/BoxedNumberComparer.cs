using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using ExtendedNumerics;

namespace NCalc.Helpers
{
    public class BoxedNumberComparer : IComparer<object?>, IEqualityComparer<object?>
    {
        private ComparisonOptions _comparisonOptions;
        private MathHelperOptions _mathHelperOptions;

        public BoxedNumberComparer(ComparisonOptions comparisonOptions, MathHelperOptions mathHelperOptions)
        {
            _comparisonOptions = comparisonOptions;
            _mathHelperOptions = mathHelperOptions;
        }

        public int Compare(object? x, object? y)
        {
            return EvaluationHelper.Compare(x, y, _comparisonOptions, _mathHelperOptions);
        }

        public new bool Equals(object? x, object? y)
        {
            return EvaluationHelper.Compare(x, y, _comparisonOptions, _mathHelperOptions) == 0;
        }

        public int GetHashCode(object? obj)
        {
            if (obj is null) return 0;

            if (obj is BigInteger bi)
                return bi.GetHashCode();

            if (obj is BigDecimal bd)
                return bd.GetHashCode();

            if (MathHelper.IsBoxedIntegerNumber(obj))
                return MathHelper.GetBoxedIntegerNumberAsLong(obj).GetHashCode();

            if (MathHelper.IsBoxedNumber(obj))
                return MathHelper.GetBoxedNumberAsDouble(obj).GetHashCode();

            return obj.GetHashCode();
        }
    }
}
