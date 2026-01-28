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
    {
        DynamicParameters = new Dictionary<string, ExpressionParameter>();
        Functions = new Dictionary<string, ExpressionFunction>();
    }

    public ExpressionContext(ExpressionOptions options, CultureInfo? cultureInfo, IDictionary<string, ExpressionParameter>? dynamicParameters = null, IDictionary<string, ExpressionFunction>? functions = null)
    {
        Options = options;
        CultureInfo = cultureInfo ?? CultureInfo.CurrentCulture;
        DynamicParameters = dynamicParameters ?? new Dictionary<string, ExpressionParameter>();
        Functions = functions ?? new Dictionary<string, ExpressionFunction>();
    }

    public static implicit operator ExpressionContext(ExpressionOptions options) => new()
    {
        Options = options
    };

    public static implicit operator ExpressionContext(CultureInfo cultureInfo) => new()
    {
        CultureInfo = cultureInfo
    };
}