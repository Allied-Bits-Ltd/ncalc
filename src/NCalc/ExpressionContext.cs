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
        // Names are compared ordinally: a linguistic comparer treats e.g. "ab" and "a‍b" as the same key and depends on the culture
        IEqualityComparer<string> comparer = StringComparer.Ordinal;

        DynamicParameters = dynamicParameters ?? new Dictionary<string, ExpressionParameter>(comparer);
        Functions = functions ?? new Dictionary<string, ExpressionFunction>(comparer);
    }

    public static implicit operator ExpressionContext(ExpressionOptions options) => new(options, null);

    public static implicit operator ExpressionContext(CultureInfo cultureInfo) => new(ExpressionOptions.None, cultureInfo);
}