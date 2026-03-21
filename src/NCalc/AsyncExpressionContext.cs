using NCalc.Handlers;

namespace NCalc;

public record AsyncExpressionContext : ExpressionContextBase
{
    public IDictionary<string, AsyncExpressionParameter> DynamicParameters { get; set; }

    public IDictionary<string, AsyncExpressionFunction> Functions { get; set; }

    public AsyncEvaluateParameterHandler? AsyncEvaluateParameterHandler { get; set; }

    public AsyncEvaluateFunctionHandler? AsyncEvaluateFunctionHandler { get; set; }

    public AsyncUpdateParameterHandler? AsyncUpdateParameterHandler { get; set; }

    public AsyncMatchStringHandler? AsyncMatchStringHandler { get; set; }

    public AsyncExpressionContext()
        : base()
    {
        DynamicParameters = new Dictionary<string, AsyncExpressionParameter>();
        Functions = new Dictionary<string, AsyncExpressionFunction>();
    }

    public AsyncExpressionContext(ExpressionOptions options, CultureInfo? cultureInfo, IDictionary<string, AsyncExpressionParameter>? dynamicParameters = null, IDictionary<string, AsyncExpressionFunction>? functions = null)
        : base(options, cultureInfo)
    {
        IEqualityComparer<string> comparer = cultureInfo is not null
            ? cultureInfo.CompareInfo.GetStringComparer(CompareOptions.None)
            : /*Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? StringComparer.InvariantCultureIgnoreCase : */StringComparer.InvariantCulture;

        DynamicParameters = dynamicParameters ?? new Dictionary<string, AsyncExpressionParameter>(comparer);
        Functions = functions ?? new Dictionary<string, AsyncExpressionFunction>(comparer);
    }

    public static implicit operator AsyncExpressionContext(ExpressionOptions options) => new(options, null);

    public static implicit operator AsyncExpressionContext(CultureInfo cultureInfo) => new(ExpressionOptions.None, cultureInfo);
}