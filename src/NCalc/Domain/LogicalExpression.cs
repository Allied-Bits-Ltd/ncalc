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
    /// The parts of the advanced options that ToString() uses (the flags, the list separator and the decimal separator), kept in JSON.
    /// The other advanced options (and the culture) only affect parsing and are not stored.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("AdvancedOptions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    internal SerializedAdvancedOptions? SerializedAdvancedOptionsForJson
    {
        get => _advancedOptions is null ? null : new SerializedAdvancedOptions
        {
            Flags = _advancedOptions.Flags,
            ListItemSeparator = _advancedOptions.ListItemSeparator,
            DecimalSeparator = _advancedOptions.GetDecimalSeparator()
        };
        set => _advancedOptions = value is null ? null : new AdvancedExpressionOptions(value.Flags)
        {
            ListItemSeparator = value.ListItemSeparator,
            DecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom,
            DecimalSeparator = value.DecimalSeparator ?? "."
        };
    }

    internal sealed class SerializedAdvancedOptions
    {
        public AdvExpressionOptions Flags { get; set; }

        public AdvancedExpressionOptions.ListItemSeparatorKind ListItemSeparator { get; set; }

        public string? DecimalSeparator { get; set; }
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