using NCalc.Exceptions;

namespace NCalc.Tests;

[Trait("Category", "Custom Culture")]
public class CustomCultureTests : TestBase
{
    [Fact]
    public void ShouldCorrectlyParseCustomCultureParameter()
    {
        var cultureDot = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        cultureDot.NumberFormat.NumberGroupSeparator = " ";
        var cultureComma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        cultureComma.NumberFormat.NumberDecimalSeparator = ",";
        cultureComma.NumberFormat.NumberGroupSeparator = " ";

        //use 1*[A] to avoid evaluating expression parameters as string - force numeric conversion
        /*ExecuteTest("1*[A]-[B]", 1.5);
        ExecuteTest("1*[A]+[B]", 2.5);
        ExecuteTest("1*[A]/[B]", 4d);
        ExecuteTest("1*[A]*[B]", 1d);*/
        ExecuteTest("1*[A]>[B]", true);
        ExecuteTest("1*[A]<[B]", false);

        void ExecuteTest(string formula, object expectedValue)
        {
            //Correctly evaluate with decimal dot culture and parameter with dot
            CheckResult(expectedValue, new Expression(formula, cultureDot)
            {
                Parameters =
                {
                    {"A","2.0"},
                    {"B","0.5"}
                }
            }.Evaluate(TestContext.Current.CancellationToken));

            //Correctly evaluate with decimal comma and parameter with comma
            CheckResult(expectedValue, new Expression(formula, cultureComma)
            {
                Parameters =
                {
                    {"A","2,0"},
                    {"B","0,5"}
                }
            }.Evaluate(TestContext.Current.CancellationToken));

            //combining decimal dot and comma fails
            try
            {
                new Expression(formula, cultureComma)
                {
                    Parameters =
                    {
                        {"A","2,0"},
                        {"B","0.5"}
                    }
                }.Evaluate(TestContext.Current.CancellationToken);
            }
            catch (Exception ex)
            {
                Assert.True(typeof(NCalcConversionException) == ex.GetType() || typeof(FormatException) == ex.GetType() || typeof(NCalcEvaluationException) == ex.GetType());
            }

            //combining decimal dot and comma fails
            try
            {
                new Expression(formula, cultureDot)
                {
                    Parameters =
                    {
                        {"A","2,0"},
                        {"B","0.5"}
                    }
                }.Evaluate(TestContext.Current.CancellationToken);
            }
            catch (Exception ex)
            {
                Assert.True(typeof(NCalcConversionException) == ex.GetType() || typeof(FormatException) == ex.GetType() || typeof(NCalcEvaluationException) == ex.GetType());
            }
        }
    }

    [Fact]
    public void ShouldParseInvariantCulture()
    {
        var originalCulture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            Thread.CurrentThread.CurrentCulture = culture;

            var e = new Expression("[a]<2.0", CultureInfo.InvariantCulture);
            e.Parameters["a"] = "1.7";
            Assert.Equal(true, e.Evaluate(TestContext.Current.CancellationToken));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }
    [Fact]
    public void ShouldConvertToStringUsingCultureInfo()
    {
        var originalCulture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            Thread.CurrentThread.CurrentCulture = culture;
            var context = new ExpressionContext(
                ExpressionOptions.StringConcat,
                CultureInfo.InvariantCulture);
            var expr = new Expression("[a] + 2.5", context)
            {
                Parameters = { ["a"] = 1.7 }
            };
            Assert.Equal("1.72.5", expr.Evaluate(TestContext.Current.CancellationToken));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void CurrencyFromCultureShouldUseTheConfiguredCulture()
    {
        // The symbol was read from the culture of the current thread
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var threadCulture in new[] { "sk-SK", "en-US" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(threadCulture);
                var options = new AdvancedExpressionOptions(CultureInfo.GetCultureInfo("en-US"), AdvExpressionOptions.AcceptCurrencySymbol)
                {
                    CurrencySymbolsType = AdvancedExpressionOptions.CurrencySymbolType.FromCulture
                };
                var expression = new Expression("$5.50", ExpressionOptions.NoCache, CultureInfo.GetCultureInfo("en-US")) { AdvancedOptions = options };
                Assert.Equal(5.5m, Convert.ToDecimal(expression.Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Fact]
    public void CachedParsersShouldKeepTheirSettingsApart()
    {
        // Parsers are reused per combination of settings; different decimal separators must not get each other's parser
        var comma = new AdvancedExpressionOptions(CultureInfo.InvariantCulture) { DecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom, DecimalSeparator = "," };
        var dot = new AdvancedExpressionOptions(CultureInfo.InvariantCulture);
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(1.5, Convert.ToDouble(new Expression("1,5 + 0", ExpressionOptions.NoCache) { AdvancedOptions = comma }.Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture));
            Assert.Equal(1.5, Convert.ToDouble(new Expression("1.5 + 0", ExpressionOptions.NoCache) { AdvancedOptions = dot }.Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public void SettingTheCultureShouldNotChangeSharedAdvancedOptions()
    {
        var shared = new AdvancedExpressionOptions(CultureInfo.GetCultureInfo("en-US"));
        var first = new Expression("1", ExpressionOptions.NoCache, CultureInfo.GetCultureInfo("en-US")) { AdvancedOptions = shared };
        var second = new Expression("1", ExpressionOptions.NoCache) { AdvancedOptions = shared };
        second.CultureInfo = CultureInfo.GetCultureInfo("de-DE");

        Assert.Equal("en-US", shared.CultureInfo!.Name);
        Assert.Equal("en-US", first.AdvancedOptions!.CultureInfo!.Name);
        Assert.Equal("de-DE", second.AdvancedOptions!.CultureInfo!.Name);
    }

    [Fact]
    public void CustomizedCultureWithAStandardNameShouldNotShareTheParser()
    {
        // NCALC-120: the parser cache was keyed by the culture name only (also with NoCache)
        var customized = new CultureInfo("en-GB");
        customized.DateTimeFormat.ShortDatePattern = "M/d/yyyy";
        var standard = CultureInfo.GetCultureInfo("en-GB");

        foreach (var options in new[] { ExpressionOptions.NoCache, ExpressionOptions.None })
        {
            Assert.Equal(new DateTime(2024, 1, 2), new Expression("#1/2/2024#", options, customized).Evaluate(TestContext.Current.CancellationToken));
            Assert.Equal(new DateTime(2024, 2, 1), new Expression("#1/2/2024#", options, standard).Evaluate(TestContext.Current.CancellationToken));
            Assert.Equal(new DateTime(2024, 1, 2), new Expression("#1/2/2024#", options, customized).Evaluate(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void PeriodListsShouldNotCollideInTheParserCache()
    {
        // NCALC-120: the list items were joined with ',' without escaping, so ["days,x"] and ["days", "x"] shared a parser
        static AdvancedExpressionOptions WithDayIndicators(params string[] indicators)
        {
            var options = new AdvancedExpressionOptions(AdvExpressionOptions.ParseHumanePeriods);
            options.PeriodDayIndicators.Clear();
            options.PeriodDayIndicators.AddRange(indicators);
            return options;
        }

        Assert.True(new Expression("#2 x#", ExpressionOptions.NoCache) { AdvancedOptions = WithDayIndicators("days,x") }.HasErrors(TestContext.Current.CancellationToken));
        Assert.Equal(TimeSpan.FromDays(2), new Expression("#2 x#", ExpressionOptions.NoCache) { AdvancedOptions = WithDayIndicators("days", "x") }.Evaluate(TestContext.Current.CancellationToken));
    }
}
