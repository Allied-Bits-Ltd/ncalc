#if NET
using System.Text.Json.Serialization;
#endif
using System.Diagnostics.Contracts;

using NCalc.Visitors;
using NCalc.Parser;
using NCalc.Helpers;

namespace NCalc.Domain;

/// <summary>
/// Represents an abstract syntax tree (AST) node for logical expressions.
/// </summary>
#if NET
// The kind of each node is written in a "$type" property ("binary", "function", "identifier", "list", "ternary", "unary", "percent",
// "expressionGroup", "value", "imaginary", "vector", "statements", "functionDeclaration", "if"), as with the System.Text.Json
// polymorphism used before. A converter is used instead of that polymorphism, which writes a list or a statement sequence
// (collections) as a plain array and would lose their options
[JsonConverter(typeof(LogicalExpressionJsonConverter))]
#endif
public abstract class LogicalExpression
{
    protected ExpressionOptions _options;
    protected CultureInfo? _cultureInfo;
    protected AdvancedExpressionOptions? _advancedOptions;

    protected ExpressionLocation _location;

#if NET
    // A CultureInfo cannot be serialized (its Parent chain is cyclic), and the property is not restored on deserialization anyway
    [JsonIgnore]
#endif
    public CultureInfo? CultureInfo => _cultureInfo;

    public ExpressionLocation Location => _location;

    /// <summary>
    /// The options the expression was parsed with. They are kept in JSON, so that a deserialized expression is written by ToString()
    /// in the same dialect (e.g. "==" with <see cref="ExpressionOptions.UseCStyleAssignments"/>).
    /// </summary>
#if NET
    [JsonInclude]
#endif
    public ExpressionOptions Options
    {
        get => _options;
        private set => _options = value;
    }

#if NET
    /// <summary>
    /// The kind of the node, written first by the default JSON contract of the node's type (see <see cref="LogicalExpressionJsonConverter"/>),
    /// so that the converter writes each node directly into the writer of its parent. It is ignored on deserialization.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName(LogicalExpressionJsonConverter.TypePropertyName)]
    [JsonPropertyOrder(int.MinValue)]
    internal string JsonTypeName => LogicalExpressionJsonConverter.GetTypeName(GetType());

    /// <summary>
    /// The parts of the advanced options that ToString() uses (the flags, the list separator, the decimal separator and, with
    /// currency symbols, the currency symbol and decimal separator, as resolved from their culture), kept in JSON.
    /// The other advanced options (and the culture) only affect parsing and are not stored.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("AdvancedOptions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    internal SerializedAdvancedOptions? SerializedAdvancedOptionsForJson
    {
        get
        {
            if (_advancedOptions is null)
                return null;

            bool hasCurrency = _advancedOptions.Flags.HasFlag(AdvExpressionOptions.AcceptCurrencySymbol);
            return new SerializedAdvancedOptions
            {
                Flags = _advancedOptions.Flags,
                ListItemSeparator = _advancedOptions.ListItemSeparator,
                DecimalSeparator = _advancedOptions.GetDecimalSeparator(),
                CurrencySymbol = hasCurrency ? SerializationVisitor.GetCurrencySymbol(_advancedOptions) : null,
                CurrencyDecimalSeparator = hasCurrency ? _advancedOptions.GetCurrencyDecimalSeparator() : null,
            };
        }
        set
        {
            if (value is null)
            {
                _advancedOptions = null;
                return;
            }

            _advancedOptions = new AdvancedExpressionOptions(value.Flags)
            {
                ListItemSeparator = value.ListItemSeparator,
                DecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom,
                DecimalSeparator = value.DecimalSeparator ?? "."
            };
            // The currency symbol of the culture the expression was written with, not the one of the current culture
            if (value.CurrencySymbol is not null)
            {
                _advancedOptions.CurrencySymbolsType = AdvancedExpressionOptions.CurrencySymbolType.Custom;
                _advancedOptions.CurrencySymbol = value.CurrencySymbol;
            }
            if (!string.IsNullOrEmpty(value.CurrencyDecimalSeparator))
            {
                _advancedOptions.CurrencyDecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom;
                _advancedOptions.CurrencyDecimalSeparator = value.CurrencyDecimalSeparator!;
            }
        }
    }

    internal sealed class SerializedAdvancedOptions
    {
        public AdvExpressionOptions Flags { get; set; }

        public AdvancedExpressionOptions.ListItemSeparatorKind ListItemSeparator { get; set; }

        public string? DecimalSeparator { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CurrencySymbol { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CurrencyDecimalSeparator { get; set; }
    }
#endif

    /// <summary>
    /// The functions declared with "fn" in the expression (set on the root expression only).
    /// They are kept so that they can be registered in a context again when the parsed expression is taken from the cache.
    /// </summary>
    internal IReadOnlyList<Function>? DeclaredFunctions { get; set; }

    public LogicalExpression()
    {
        _options = ExpressionOptions.None;
        _location = ExpressionLocation.Empty;
    }

    public LogicalExpression(ExpressionLocation location)
    {
        _options = ExpressionOptions.None;
        _location = location;
    }

    protected LogicalExpression(ExpressionOptions options, CultureInfo? cultureInfo, AdvancedExpressionOptions? advancedOptions, ExpressionLocation location)
    {
        _location = location;
        SetOptions(options, cultureInfo, advancedOptions);
    }

    public LogicalExpression SetOptions(ExpressionOptions options, CultureInfo? cultureInfo, AdvancedExpressionOptions? advancedOptions)
    {
        _options = options;
        _cultureInfo = cultureInfo;
        _advancedOptions = advancedOptions;
        return this;
    }

    public LogicalExpression SetOptions(ExpressionOptions options, CultureInfo? cultureInfo, AdvancedExpressionOptions? advancedOptions, ExpressionLocation location)
    {
        _location = location;
        _options = options;
        _cultureInfo = cultureInfo;
        _advancedOptions = advancedOptions;
        return this;
    }

    public LogicalExpression SetLocation(ExpressionLocation location)
    {
        _location = location;
        return this;
    }

    public override string ToString()
    {
        var serializer = new SerializationVisitor(new SerializationContext(_options, _cultureInfo, _advancedOptions));
        return Accept(serializer).TrimEnd(' ');
    }

    [Pure]
    public abstract T Accept<T>(ILogicalExpressionVisitor<T> visitor, CancellationToken cancellationToken = default);

    [Pure]
    internal abstract T AcceptNoRecurse<T>(ILogicalExpressionNoRecurseVisitor<T> visitor, ExpressionTask<T> task, CancellationToken cancellationToken = default);
}