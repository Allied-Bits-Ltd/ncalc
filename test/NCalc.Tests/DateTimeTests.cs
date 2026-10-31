using NCalc.Exceptions;

namespace NCalc.Tests;

[Trait("Category", "DateTime")]
public class DateTimeTests
{
    [Fact]
    public void Should_Parse_Time()
    {
        var timeSeparator = CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator;
        var expr = new Expression($"#20{timeSeparator}42{timeSeparator}12#");
        Assert.Equal(new TimeSpan(20, 42, 12), expr.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Parse_Date()
    {
        var dateSeparator = CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator;
        string exprStr = "";
        switch (CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern[0])
        {
            case 'M': exprStr = $"#01{dateSeparator}01{dateSeparator}2001#"; break;
            case 'd': exprStr = $"#01{dateSeparator}01{dateSeparator}2001#"; break;
            case 'y': exprStr = $"#2001{dateSeparator}01{dateSeparator}01#"; break;
        }
        Assert.False(string.IsNullOrEmpty(exprStr));

        var expr = new Expression(exprStr);
        Assert.Equal(new DateTime(2001, 1, 1), expr.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Parse_Date_Time()
    {
        var timeSeparator = CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator;
        var dateSeparator = CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator;
        string exprStr = "";
        switch (CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern[0])
        {
            case 'M': exprStr = $"#12{dateSeparator}31{dateSeparator}2022 08{timeSeparator}00{timeSeparator}00#"; break;
            case 'd': exprStr = $"#31{dateSeparator}12{dateSeparator}2022 08{timeSeparator}00{timeSeparator}00#"; break;
            case 'y': exprStr = $"#2022{dateSeparator}12{dateSeparator}31 08{timeSeparator}00{timeSeparator}00#"; break;
        }
        Assert.False(string.IsNullOrEmpty(exprStr));
        Assert.Equal(new DateTime(2022, 12, 31, 8, 0, 0), new Expression(exprStr).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Parse_Date_Iso()
    {
        string exprStr = "#2026-02-01#";
        var expr = new Expression(exprStr);
        Assert.Equal(new DateTime(2026, 2, 1), expr.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Parse_Date_Time_Iso()
    {
        string exprStr = "#2026-02-01T08:15:27Z#";
        Assert.Equal(new DateTime(2026, 02, 1, 8, 15, 27), new Expression(exprStr).Evaluate(TestContext.Current.CancellationToken));

        exprStr = "#2026-02-01T08:15:27.550Z#";
        Assert.Equal(new DateTime(2026, 02, 1, 8, 15, 27, 550), new Expression(exprStr).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Fail_With_Wrong_DateTime_Separator()
    {
        var trueTimeSeparator = CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator;
        var trueDateSeparator = CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator;

        var timeSeparator = trueTimeSeparator == ":" ? "." : ":";
        var dateSeparator = trueDateSeparator == "." ? "/" : ".";

        string exprStr = "";
        switch (CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern[0])
        {
            case 'M': exprStr = $"#12{dateSeparator}31{dateSeparator}2022 08{timeSeparator}00{timeSeparator}00#"; break;
            case 'd': exprStr = $"#31{dateSeparator}12{dateSeparator}2022 08{timeSeparator}00{timeSeparator}00#"; break;
            case 'y': exprStr = $"#2022{dateSeparator}12{dateSeparator}31 08{timeSeparator}00{timeSeparator}00#"; break;
        }
        Assert.False(string.IsNullOrEmpty(exprStr));

        Assert.Throws<NCalcParserException>(() => new Expression(exprStr).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldHandleRuntimeCultureChange()
    {
        var oldCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        try
        {
            var expr = new Expression("#05/27/2025 12:00:00#", ExpressionOptions.None);
            var res = expr.Evaluate(TestContext.Current.CancellationToken);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var expr2 = new Expression("#27.05.2025 12:00:00#");
            var res2 = expr.Evaluate(TestContext.Current.CancellationToken);

            var dt = new DateTime(2025, 05, 27, 12, 0, 0);

            Assert.Equal(dt, res);
            Assert.Equal(dt, res2);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Fact]
    public void ShouldHandleSpecifiedExpressionCulture()
    {
        var oldCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        try
        {
            var expr = new Expression("#05/27/2025 12:00:00#", ExpressionOptions.None);
            var res = expr.Evaluate(TestContext.Current.CancellationToken);

            var ruCulture = CultureInfo.GetCultureInfo("ru-RU");
            var expr2 = new Expression("#27.05.2025 12:00:00#", ExpressionOptions.None, ruCulture);
            var res2 = expr.Evaluate(TestContext.Current.CancellationToken);

            var dt = new DateTime(2025, 05, 27, 12, 0, 0);

            Assert.Equal(dt, res);
            Assert.Equal(dt, res2);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Fact]
    public void ShouldAddTicksToDateTime()
    {
        var expr = new Expression("#2026-01-01T00:00:00Z# + 86400 * 10000000", ExpressionOptions.SupportTimeOperations | ExpressionOptions.UseBigNumbers);
        var res = expr.Evaluate(TestContext.Current.CancellationToken);

        var dt = new DateTime(2026, 01, 02, 0, 0, 0);

        Assert.Equal(dt, res);
    }

    [Fact]
    public void ShouldSubtractTicksFromDateTime()
    {
        var expr = new Expression("#2026-01-02T00:00:00Z# - 86400 * 10000000", ExpressionOptions.SupportTimeOperations | ExpressionOptions.UseBigNumbers);
        var res = expr.Evaluate(TestContext.Current.CancellationToken);

        var dt = new DateTime(2026, 01, 01, 0, 0, 0);

        Assert.Equal(dt, res);
    }

    [Fact]
    public void ShouldAddTicksToTimeSpan()
    {
        var expr = new Expression("#2026-01-02T00:00:00Z# - #2026-01-01T00:00:00Z# + 86400 * 10000000", ExpressionOptions.SupportTimeOperations | ExpressionOptions.UseBigNumbers);
        var res = expr.Evaluate(TestContext.Current.CancellationToken);

        var dt = new TimeSpan(2, 0, 0, 0);

        Assert.Equal(dt, res);
    }

    [Fact]
    public void ShouldSubtractTicksFromTimeSpan()
    {
        var expr = new Expression("#2026-01-03T00:00:00Z# - #2026-01-01T00:00:00Z# - 86400 * 10000000", ExpressionOptions.SupportTimeOperations | ExpressionOptions.UseBigNumbers);
        var res = expr.Evaluate(TestContext.Current.CancellationToken);

        var dt = new TimeSpan(1, 0, 0, 0);

        Assert.Equal(dt, res);
    }

    [Fact]
    public void ShouldMultiplyTimeSpan()
    {
        var expr = new Expression("(#2026-01-02T00:00:00Z# - #2026-01-01T00:00:00Z#) * 2", ExpressionOptions.SupportTimeOperations | ExpressionOptions.UseBigNumbers);
        var res = expr.Evaluate(TestContext.Current.CancellationToken);

        var dt = new TimeSpan(2, 0, 0, 0);

        Assert.Equal(dt, res);
    }

    [Fact]
    public void ShouldDivideTimeSpan()
    {
        var expr = new Expression("(#2026-01-03T00:00:00Z# - #2026-01-01T00:00:00Z#) / 2", ExpressionOptions.SupportTimeOperations | ExpressionOptions.UseBigNumbers | ExpressionOptions.DecimalAsDefault);
        var res = expr.Evaluate(TestContext.Current.CancellationToken);

        var dt = new TimeSpan(1, 0, 0, 0);

        Assert.Equal(dt, res);
    }

    [Theory]
    [InlineData("1", 1000000L)]
    [InlineData("12", 1200000L)]
    [InlineData("123", 1230000L)]
    [InlineData("1234", 1234000L)]
    [InlineData("12345", 1234500L)]
    [InlineData("123456", 1234560L)]
    [InlineData("1234567", 1234567L)]
    public void IsoDateTimeFractionShouldBeExact(string fraction, long expectedTicks)
    {
        var result = (DateTime)new Expression($"#2001-01-01T00:00:00.{fraction}#", ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken)!;
        Assert.Equal(expectedTicks, result.Ticks % TimeSpan.TicksPerSecond);
    }

    [Theory]
    [InlineData("#25:00:00#", 25, 0)]
    [InlineData("#100:30#", 100, 30)]
    [InlineData("#24:00:00#", 24, 0)]
    [InlineData("#1.23:00:00#", 47, 0)]
    public void TimeLiteralShouldNotReadHoursAsDays(string expression, int hours, int minutes)
    {
        Assert.Equal(new TimeSpan(hours, minutes, 0), new Expression(expression, ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("#1.25:00:00#")]
    [InlineData("#10:60:00#")]
    [InlineData("#00:00:61#")]
    public void TimeLiteralWithOutOfRangePartsShouldBeRejected(string expression)
    {
        Assert.ThrowsAny<NCalcException>(() => new Expression(expression, ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RelativeDateLiteralsShouldBeComputedAtEvaluation()
    {
        var options = new AdvancedExpressionOptions(AdvExpressionOptions.ParseHumanePeriods);
        var expression = new Expression("#now#") { AdvancedOptions = options };
        var first = (DateTime)expression.Evaluate(TestContext.Current.CancellationToken)!;
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Both the same instance and a new instance that gets the parsed expression from the cache must see the later time
        Assert.True((DateTime)expression.Evaluate(TestContext.Current.CancellationToken)! > first);
        Assert.True((DateTime)new Expression("#now#") { AdvancedOptions = options }.Evaluate(TestContext.Current.CancellationToken)! > first);
        Assert.True(new Expression("#now#") { AdvancedOptions = options }.ToLambda<DateTime>()() > first);
        Assert.Equal(DateTime.Today, new Expression("#today#") { AdvancedOptions = options }.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("#2001-01-01#")]
    [InlineData("# a comment\n#2001-01-01#")]
    [InlineData("#2001-01-01# # a comment")]
    [InlineData("#2001-01-01#  #a comment")]
    public void DateLiteralsShouldWorkWithPythonComments(string expression)
    {
        Assert.Equal(new DateTime(2001, 1, 1), new Expression(expression, ExpressionOptions.SupportPythonComments | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void PythonCommentsShouldStillBeComments()
    {
        Assert.Equal(3, new Expression("1 + 2 # comment", ExpressionOptions.SupportPythonComments | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(3, new Expression("1 + 2 #comment", ExpressionOptions.SupportPythonComments | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }
}
