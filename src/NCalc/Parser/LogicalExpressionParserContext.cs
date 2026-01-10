using NCalc.Domain;
using Parlot;
using Parlot.Fluent;

namespace NCalc.Parser;

public sealed class LogicalExpressionParserContext : ParseContext
{
    private AdvancedExpressionOptions? _advancedOptions;

    public ExpressionOptions Options { get; }

    public IDictionary<string, Function> UserFunctions { get; } = new Dictionary<string, Function>();

    public AdvancedExpressionOptions? AdvancedOptions { get => _advancedOptions;
        internal set
        {
            _advancedOptions = value;
            if (value != null)
                SetupSecondaryProperties();
        }
    }

    public CultureInfo CultureInfo { get; }

    public LogicalExpressionParserContext(string text, ExpressionOptions options, CultureInfo? cultureInfo, CancellationToken cancellationToken)
        : base(new Scanner(text), cancellationToken)
    {
        Options = options;
        CultureInfo = cultureInfo ?? CultureInfo.CurrentCulture;
        SetupSecondaryProperties();
    }

    public LogicalExpressionParserContext(string text, ExpressionOptions options, CultureInfo? cultureInfo)
        : this(text, options, cultureInfo, default(CancellationToken))
    {
        Options = options;
        CultureInfo = cultureInfo ?? CultureInfo.CurrentCulture;
        SetupSecondaryProperties();
    }

    public LogicalExpressionParserContext(string text, ExpressionOptions options, CancellationToken cancellationToken)
        : this(text, options, CultureInfo.CurrentCulture, cancellationToken)
    {
    }

    public LogicalExpressionParserContext(string text, ExpressionOptions options)
        : this(text, options, CultureInfo.CurrentCulture)
    {
    }

    public LogicalExpressionParserContext(string text, ExpressionOptions options, CultureInfo cultureInfo, AdvancedExpressionOptions? advancedOptions, CancellationToken cancellationToken)
        : this(text, options, cultureInfo, cancellationToken)
    {
        AdvancedOptions = advancedOptions;
    }

    public LogicalExpressionParserContext(string text, ExpressionOptions options, CultureInfo cultureInfo, AdvancedExpressionOptions? advancedOptions)
        : this(text, options, cultureInfo)
    {
        AdvancedOptions = advancedOptions;
    }

    public LogicalExpressionParserContext(string text, ExpressionOptions options, AdvancedExpressionOptions? advancedOptions, CancellationToken cancellationToken)
        : this(text, options, CultureInfo.CurrentCulture, cancellationToken)
    {
        AdvancedOptions = advancedOptions;
    }

    public LogicalExpressionParserContext(string text, ExpressionOptions options, AdvancedExpressionOptions? advancedOptions)
        : this(text, options, CultureInfo.CurrentCulture)
    {
        AdvancedOptions = advancedOptions;
    }

    private void SetupSecondaryProperties()
    {
        AcceptUnderscores = AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.AcceptUnderscoresInNumbers) ?? false;
        UnsignedHexBinOct = Options.HasFlag(ExpressionOptions.HexBinOctAreUnsigned);
        UseBigNumbers = Options.HasFlag(ExpressionOptions.UseBigNumbers);

        if (Options.HasFlag(ExpressionOptions.SupportCStyleComments) || Options.HasFlag(ExpressionOptions.SupportPythonComments))
            this.WhiteSpaceParser = new NCalc.Parser.WhiteSpaceParser(Options);
    }

    public bool AcceptUnderscores { get; private set; }
    public bool UnsignedHexBinOct { get; private set; }
    public bool UseBigNumbers { get; private set; }
}