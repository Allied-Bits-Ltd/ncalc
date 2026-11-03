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

    // Set in the context of the body of a user function: the arguments of the call and of the calls of user functions that it is nested in
    internal UserFunctionScope? FunctionScope { get; set; }

    // The number of calls of user functions that are in progress around an evaluation in this context: 0 for an expression, 1 for the body
    // of a user function called by it, 2 for the body of a function called by that body, etc.
    internal int UserFunctionCallDepth { get; set; }

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
        // Names are compared ordinally: a linguistic comparer treats e.g. "ab" and "a‍b" as the same key and depends on the culture
        IEqualityComparer<string> comparer = StringComparer.Ordinal;

        StaticParameters = new Dictionary<string, object?>(comparer);

        UserFunctions =  new Dictionary<string, Function>(comparer);
    }
}