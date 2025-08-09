#if NET
using System.Text.Json.Serialization;
#endif
using System.Diagnostics.Contracts;

using NCalc.Visitors;
using NCalc.Parser;

namespace NCalc.Domain;

/// <summary>
/// Represents an abstract syntax tree (AST) node for logical expressions.
/// </summary>
#if NET
[JsonPolymorphic]
[JsonDerivedType(typeof(BinaryExpression), typeDiscriminator: "binary")]
[JsonDerivedType(typeof(Function), typeDiscriminator: "function")]
[JsonDerivedType(typeof(Identifier), typeDiscriminator: "identifier")]
[JsonDerivedType(typeof(LogicalExpressionList), typeDiscriminator: "list")]
[JsonDerivedType(typeof(TernaryExpression), typeDiscriminator: "ternary")]
[JsonDerivedType(typeof(UnaryExpression), typeDiscriminator: "unary")]
[JsonDerivedType(typeof(PercentExpression), typeDiscriminator: "percent")]
[JsonDerivedType(typeof(ExpressionGroup), typeDiscriminator: "expressionGroup")]
[JsonDerivedType(typeof(ValueExpression), typeDiscriminator: "value")]
#endif
public abstract class LogicalExpression
{
    protected ExpressionOptions _options;
    protected CultureInfo? _cultureInfo;
    protected AdvancedExpressionOptions? _advancedOptions;

    protected ExpressionLocation _location;

    public ExpressionLocation Location => _location;

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
}