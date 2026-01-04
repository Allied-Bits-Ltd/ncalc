using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCalc.Helpers
{
    public class BoxedNumberComparer : IComparer<object?>
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
    }
}
