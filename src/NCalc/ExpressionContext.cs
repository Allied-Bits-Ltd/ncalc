using NCalc.Handlers;

namespace NCalc;

public record ExpressionContext : ExpressionContextBase
{
    public IDictionary<string, ExpressionParameter> DynamicParameters { get; set; }

    public IDictionary<string, ExpressionFunction> Functions { get; set; }

    public EvaluateParameterHandler? EvaluateParameterHandler { get; set; }
    public EvaluateFunctionHandler? EvaluateFunctionHandler { get; set; }
    public UpdateParameterHandler? UpdateParameterHandler { get; set; }
    public MatchStringHandler? MatchStringHandler { get; set; }

    public ExpressionContext()
        : base()
    {
        DynamicParameters = new Dictionary<string, ExpressionParameter>();
        Functions = new Dictionary<string, ExpressionFunction>();
    }

    public ExpressionContext(ExpressionOptions options, CultureInfo? cultureInfo, IDictionary<string, ExpressionParameter>? dynamicParameters = null, IDictionary<string, ExpressionFunction>? functions = null)
        : base(options, cultureInfo)
    {
        IEqualityComparer<string> comparer = cultureInfo is not null
            ? cultureInfo.CompareInfo.GetStringComparer(CompareOptions.None)
            : /*Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? StringComparer.InvariantCultureIgnoreCase : */StringComparer.InvariantCulture;

        DynamicParameters = dynamicParameters ?? new Dictionary<string, ExpressionParameter>(comparer);
        Functions = functions ?? new Dictionary<string, ExpressionFunction>(comparer);
    }

    public static implicit operator ExpressionContext(ExpressionOptions options) => new(options, null);

    public static implicit operator ExpressionContext(CultureInfo cultureInfo) => new(ExpressionOptions.None, cultureInfo);
}