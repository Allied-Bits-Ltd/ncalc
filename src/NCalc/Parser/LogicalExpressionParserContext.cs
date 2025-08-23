using Parlot;
using Parlot.Fluent;

namespace NCalc.Parser;

public sealed class LogicalExpressionParserContext : ParseContext
{
    private AdvancedExpressionOptions? _advancedOptions;

    public ExpressionOptions Options { get; }

    public AdvancedExpressionOptions? AdvancedOptions { get => _advancedOptions;
        internal set
        {
            _advancedOptions = value;
            if (value != null)
                SetupSecondaryProperties();
        }
    }

    public CultureInfo CultureInfo { get; }

    public LogicalExpressionParserContext(string text, ExpressionOptions options) : base(new Scanner(text))
    {
        Options = options;
        CultureInfo = CultureInfo.CurrentCulture;
        SetupSecondaryProperties();
    }

    public LogicalExpressionParserContext(string text, ExpressionOptions options, CultureInfo cultureInfo) : this(text, options)
    {
        CultureInfo = cultureInfo;
    }

    public LogicalExpressionParserContext(string text, ExpressionOptions options, CultureInfo cultureInfo, AdvancedExpressionOptions? advancedOptions) : this(text, options)
    {
        CultureInfo = cultureInfo;
        AdvancedOptions = advancedOptions;
    }

    public LogicalExpressionParserContext(string text, ExpressionOptions options, AdvancedExpressionOptions? advancedOptions) : this(text, options)
    {
        AdvancedOptions = advancedOptions;
    }

    private void SetupSecondaryProperties()
    {
        AcceptUnderscores = AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.AcceptUnderscoresInNumbers) ?? false;
        UnsignedHexBinOct = Options.HasFlag(ExpressionOptions.HexBinOctAreUnsigned);
        UseBigNumbers = Options.HasFlag(ExpressionOptions.UseBigNumbers);
    }

    public bool AcceptUnderscores { get; private set; }
    public bool UnsignedHexBinOct { get; private set; }
    public bool UseBigNumbers { get; private set; }
}