using System;
using System.Collections.Generic;
using System.Text;

namespace NCalc.Tests.AOT
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var tests = new AdvFeatureTests();
            tests.SerializeFactorialExpressionsTest();
            tests.SerializePercentExpressionsTest();
            tests.ShouldAcceptCurrencyCulture("500.50", 0, (decimal) 500.50);
            tests.ShouldAddSubtractDateAndTime("#2025/06/05# + #08:00:00#", new int[] { 2025, 6, 5, 8, 0, 0 });

            tests.ShouldCalculateBigIntegers("1 + 110680464442257309690 + 1", "110680464442257309692");
            tests.ShouldCalculateBigIntegers("1 - 110680464442257309690", "-110680464442257309689");

            tests.ShouldCalculateIntegerDiv("8.5 \\ 2.5", (long)4);
            tests.ShouldCalculateIntegerDiv("8.5 div 2.5", (long)4);
            tests.ShouldCalculateIntegerDiv("8.5 // 2.5", (long)3);

            tests.ShouldCalculatePercentAsNumber("20*5%", 1);
            tests.ShouldCalculatePercentAsNumber("20/5%", 400);
            tests.ShouldCalculatePercentAsNumber("20/2.5%", 800);
            tests.ShouldCalculatePercentAsNumber("100+5%", 105);
            tests.ShouldCalculatePercentAsNumber("100+(3+2)%", 105);

            tests.ShouldCalculatePercentAsPercent("5%+2%", "7%");
            tests.ShouldCalculatePercentAsPercent("(2/5)%", "0.4%");
            tests.ShouldCalculatePercentAsPercent("3.5% + 2.5%", "6%");
            tests.ShouldCalculatePercentAsPercent("5%-2%", "3%");
            tests.ShouldCalculatePercentAsPercent("5%*2", "10%");
            tests.ShouldCalculatePercentAsPercent("10%/2", "5%");
            tests.ShouldCalculatePercentAsPercent("10%/2 + 3%*3", "14%");
            tests.ShouldCalculatePercentAsPercent("%(25,50)", "50%");
            tests.ShouldCalculatePercentAsPercent("PercentDiff(80,60)", "-25%");
            tests.ShouldCalculatePercentAsPercent("PercentDiff(50,75)", "50%");
        }
    }
}
