using NCalc.Domain;
using NCalc.Helpers;

namespace NCalc;

public abstract record ExpressionContextBase
{
    public ExpressionOptions Options { get; set; } = ExpressionOptions.None;

    public AdvancedExpressionOptions? AdvancedOptions { get; set; } = null;

    public CultureInfo CultureInfo { get; set; } = CultureInfo.CurrentCulture;

    public IDictionary<string, object?> StaticParameters { get; set; }

    public IDictionary<string, Function> UserFunctions { get; private set; }

    public static implicit operator MathHelperOptions(ExpressionContextBase context)
    {
        return new MathHelperOptions(context.CultureInfo, context.Options);
    }

    public static implicit operator ComparisonOptions(ExpressionContextBase context)
    {
        return new ComparisonOptions(context.CultureInfo, context.Options);
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public ExpressionContextBase()
    {
        CreateDictionaries();
    }

    public ExpressionContextBase(ExpressionOptions options, CultureInfo? cultureInfo)
    {
        Options = options;
        CultureInfo = cultureInfo ?? CultureInfo.CurrentCulture;

        CreateDictionaries(cultureInfo);
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private void CreateDictionaries(CultureInfo? cultureInfo = null)
    {
        IEqualityComparer<string> comparer = cultureInfo is not null
            ? cultureInfo.CompareInfo.GetStringComparer(CompareOptions.None)
            : /*Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? StringComparer.InvariantCultureIgnoreCase : */StringComparer.InvariantCulture;

        StaticParameters = new Dictionary<string, object?>(comparer);

        UserFunctions =  new Dictionary<string, Function>(comparer);
    }
}