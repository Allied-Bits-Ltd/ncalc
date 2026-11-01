using System.Reflection;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Helpers;
using NCalc.Parser;
using NCalc.Reflection;

using Linq = System.Linq.Expressions;
using LinqExpression = System.Linq.Expressions.Expression;
using LinqParameterExpression = System.Linq.Expressions.ParameterExpression;

namespace NCalc.Visitors;

/// <summary>
/// Compiles a <see cref="LogicalExpression"/> into a LINQ expression tree. The operators and built-in functions are compiled as calls
/// to the helper methods that the evaluators use (see <see cref="LambdaRuntime"/>), so a compiled lambda gives the values that Evaluate() gives.
/// Assignments (which need a context object to assign to), loops, <c>return</c>, indexes and function declarations are not supported
/// and throw <see cref="NotSupportedException"/> when the lambda is created.
/// </summary>
public sealed class LambdaExpressionVisitor : ILogicalExpressionVisitor<LinqExpression>
{
    private readonly IDictionary<string, object?>? _parameters;
    private readonly LinqExpression? _context;
    private readonly ExpressionOptions _options;
    private readonly bool _caseInsensitiveStringComparer;

    private readonly ExpressionContext _expressionContext;
    private readonly LinqExpression _expressionContextConstant;

    private static readonly LinqExpression NullConstant = LinqExpression.Constant(null, typeof(object));

    public LambdaExpressionVisitor(LinqParameterExpression? context, ExpressionContext? expressionContext, IDictionary<string, object?>? parameters, ExpressionOptions options)
    {
        if (context != null)
            _context = context;
        if (parameters != null)
            _parameters = parameters;

        _options = options;
        _caseInsensitiveStringComparer = _options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer);

        _expressionContext = expressionContext ?? new ExpressionContext(options, CultureInfo.CurrentCulture);
        _expressionContextConstant = LinqExpression.Constant(_expressionContext, typeof(ExpressionContext));
    }

    public LinqExpression Visit(TernaryExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var conditional = Box(expression.LeftExpression.Accept(this, cancellationToken));
        var ifTrue = Box(expression.MiddleExpression.Accept(this, cancellationToken));
        var ifFalse = Box(expression.RightExpression.Accept(this, cancellationToken));

        // As in the evaluators, a null condition gives null, and the result is the value of the chosen branch with its own type
        var condition = LinqExpression.Variable(typeof(object), "condition");
        return LinqExpression.Block(
            [condition],
            LinqExpression.Assign(condition, conditional),
            LinqExpression.Condition(
                CallRuntime(nameof(LambdaRuntime.IsNull), condition, _expressionContextConstant),
                NullConstant,
                LinqExpression.Condition(
                    CallRuntime(nameof(LambdaRuntime.ToBoolean), condition, LinqExpression.Constant("Ternary"), NodeConstant(expression.LeftExpression), _expressionContextConstant),
                    ifTrue,
                    ifFalse)));
    }

    public LinqExpression Visit(BinaryExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();

        switch (expression.Type)
        {
            case BinaryExpressionType.Assignment:
            case BinaryExpressionType.PlusAssignment:
            case BinaryExpressionType.MinusAssignment:
            case BinaryExpressionType.MultiplyAssignment:
            case BinaryExpressionType.DivAssignment:
            case BinaryExpressionType.AndAssignment:
            case BinaryExpressionType.OrAssignment:
            case BinaryExpressionType.XOrAssignment:
                return Assignment(expression, cancellationToken);

            case BinaryExpressionType.WhileLoop:
                throw new NotSupportedException("Loops are not supported in lambda expressions.");

            case BinaryExpressionType.IndexAccess:
            case BinaryExpressionType.RangeIndex:
                throw new NotSupportedException("Indexed access to parameters is not supported for Lambda expressions. Please submit an issue to https://github.com/Allied-Bits-Ltd/ncalc/issues if you need this support.");

            case BinaryExpressionType.Unknown:
                throw new ArgumentOutOfRangeException(nameof(expression), expression.Type, null);
        }

        var left = Box(expression.LeftExpression.Accept(this, cancellationToken));
        var right = Box(expression.RightExpression.Accept(this, cancellationToken));
        var node = LinqExpression.Constant(expression);
        var leftValue = LinqExpression.Variable(typeof(object), "left");

        switch (expression.Type)
        {
            case BinaryExpressionType.Equal:
            case BinaryExpressionType.NotEqual:
            case BinaryExpressionType.Greater:
            case BinaryExpressionType.GreaterOrEqual:
            case BinaryExpressionType.Less:
            case BinaryExpressionType.LessOrEqual:
                // Both operands are evaluated, null included
                return CallRuntime(nameof(LambdaRuntime.Compare), left, right, node, _expressionContextConstant);

            case BinaryExpressionType.And:
            case BinaryExpressionType.Or:
            case BinaryExpressionType.XOr:
            {
                // The right operand is evaluated only when the left one does not decide the result
                var decided = LinqExpression.Variable(typeof(object), "decided");
                return LinqExpression.Block(
                    [leftValue, decided],
                    LinqExpression.Assign(leftValue, left),
                    LinqExpression.Assign(decided, CallRuntime(nameof(LambdaRuntime.LogicalLeft), leftValue, node, _expressionContextConstant)),
                    LinqExpression.Condition(
                        LinqExpression.ReferenceEqual(decided, LinqExpression.Constant(LambdaRuntime.Undecided)),
                        CallRuntime(nameof(LambdaRuntime.LogicalRight), leftValue, right, node, _expressionContextConstant),
                        decided));
            }

            default:
                // A null left operand gives null without evaluating the right one
                return LinqExpression.Block(
                    [leftValue],
                    LinqExpression.Assign(leftValue, left),
                    LinqExpression.Condition(
                        CallRuntime(nameof(LambdaRuntime.IsNull), leftValue, _expressionContextConstant),
                        NullConstant,
                        CallRuntime(nameof(LambdaRuntime.Binary), leftValue, right, node, _expressionContextConstant)));
        }
    }

    public LinqExpression Visit(UnaryExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        if (expression.Type == UnaryExpressionType.Return)
            throw new NotSupportedException("The return statement is not supported in lambda expressions.");

        var operand = Box(expression.Expression.Accept(this, cancellationToken));
        return CallRuntime(nameof(LambdaRuntime.Unary), operand, LinqExpression.Constant(expression), _expressionContextConstant);
    }

    public LinqExpression Visit(PercentExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var value = Box(expression.Expression.Accept(this, cancellationToken));
        return CallRuntime(nameof(LambdaRuntime.Percent), value, _expressionContextConstant);
    }

    public LinqExpression Visit(ImaginaryNumberExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var value = Box(expression.Expression.Accept(this, cancellationToken));
        return CallRuntime(nameof(LambdaRuntime.Imaginary), value, LinqExpression.Constant(expression), _expressionContextConstant);
    }

    public LinqExpression Visit(VectorExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var components = LinqExpression.NewArrayInit(typeof(object), expression.Expressions.Select(e => Box(e.Accept(this, cancellationToken))));
        return CallRuntime(nameof(LambdaRuntime.Vector), components, LinqExpression.Constant(expression));
    }

    public LinqExpression Visit(ExpressionGroup group, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        return group.Expression.Accept(this, cancellationToken);
    }

    public LinqExpression Visit(ValueExpression expression, CancellationToken cancellationToken = default)
    {
        // A relative date or time literal (#now#) is computed whenever the compiled lambda runs
        if (expression.ValueFactory is { } valueFactory)
            return LinqExpression.Convert(LinqExpression.Invoke(LinqExpression.Constant(valueFactory)), expression.Value!.GetType());

        return LinqExpression.Constant(expression.Value);
    }

    public LinqExpression Visit(FunctionCall function, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var functionName = function.Identifier.Name;

        // Functions of the expression (fn declarations and the Functions dictionary) are called by the evaluators before the built-in ones
        var ignoreIdentifierCase = _options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup);
        if (_expressionContext.UserFunctions.Keys.Concat(_expressionContext.Functions.Keys)
            .Any(name => name.Equals(functionName, ignoreIdentifierCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
            throw new NotSupportedException($"The function '{functionName}' of the expression is not supported in lambda expressions.");

        var comparison = _options.HasFlag(ExpressionOptions.IgnoreCaseAtBuiltInFunctions) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var parameters = function.Parameters;

        // The functions that do not evaluate all their arguments
        if (functionName.Equals("if", comparison) || functionName.Equals("iff", comparison))
            return IfFunction(function, cancellationToken);
        if (functionName.Equals("ifs", comparison))
            return IfsFunction(function, cancellationToken);
        if (functionName.Equals("in", comparison))
            return InFunction(function, cancellationToken);

        var args = new LinqExpression[parameters.Count];
        for (var i = 0; i < parameters.Count; i++)
        {
            args[i] = parameters[i].Accept(this, cancellationToken);
        }

        //Context methods take precedence over built-in functions because they're user-customizable.
        var mi = FindMethod(functionName, args, ignoreIdentifierCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture);
        if (mi != null)
        {
            return LinqExpression.Call(_context, mi.MethodInfo, mi.PreparedArguments);
        }

        var functionNode = LinqExpression.Constant(function);

        if (functionName.Equals("%", StringComparison.Ordinal) || functionName.Equals("PercentOf", comparison))
            return args.Length == 2 ? PercentFunction(nameof(LambdaRuntime.PercentOf), args) : ThrowEvaluationException("PercentOf() takes exactly 2 arguments", function.Location);
        if (functionName.Equals("PercentDiff", comparison))
            return args.Length == 2 ? PercentFunction(nameof(LambdaRuntime.PercentDiff), args) : ThrowEvaluationException("PercentDiff() takes exactly 2 arguments", function.Location);

        foreach (var unaryFunction in LambdaRuntime.UnaryFunctions)
        {
            if (!functionName.Equals(unaryFunction.Key, comparison))
                continue;
            if (args.Length != 1)
                return ThrowEvaluationException($"{(unaryFunction.Key == "Trunc" ? "Truncate" : unaryFunction.Key)}() takes exactly 1 argument", function.Location);

            // A null argument gives a null result, as with the operators
            var arg0 = LinqExpression.Variable(typeof(object), "arg0");
            return LinqExpression.Block(
                [arg0],
                LinqExpression.Assign(arg0, Box(args[0])),
                LinqExpression.Condition(
                    LinqExpression.ReferenceEqual(arg0, NullConstant),
                    NullConstant,
                    LinqExpression.Invoke(LinqExpression.Constant(unaryFunction.Value), arg0, _expressionContextConstant)));
        }

        foreach (var binaryFunction in LambdaRuntime.BinaryFunctions)
        {
            if (!functionName.Equals(binaryFunction.Key, comparison))
                continue;
            if (args.Length != 2)
                return ThrowEvaluationException($"{binaryFunction.Key}() takes exactly 2 arguments", function.Location);

            // Both arguments are evaluated; a null argument gives a null result
            var arg0 = LinqExpression.Variable(typeof(object), "arg0");
            var arg1 = LinqExpression.Variable(typeof(object), "arg1");
            return LinqExpression.Block(
                [arg0, arg1],
                LinqExpression.Assign(arg0, Box(args[0])),
                LinqExpression.Assign(arg1, Box(args[1])),
                LinqExpression.Condition(
                    LinqExpression.OrElse(LinqExpression.ReferenceEqual(arg0, NullConstant), LinqExpression.ReferenceEqual(arg1, NullConstant)),
                    NullConstant,
                    LinqExpression.Invoke(LinqExpression.Constant(binaryFunction.Value), arg0, arg1, _expressionContextConstant)));
        }

        if (functionName.Equals("MakeList", comparison))
        {
            if (args.Length is not (1 or 2))
                return ThrowEvaluationException("MakeList() takes 1 or 2 arguments", function.Location);

            // The size is checked before the value is evaluated
            var size = LinqExpression.Variable(typeof(long), "size");
            return LinqExpression.Block(
                [size],
                LinqExpression.Assign(size, CallRuntime(nameof(LambdaRuntime.MakeSize), Box(args[0]), LinqExpression.Constant("List size"), LinqExpression.Constant("MakeList"), functionNode)),
                CallRuntime(nameof(LambdaRuntime.MakeList), size, args.Length == 2 ? Box(args[1]) : NullConstant));
        }

        if (functionName.Equals("MakeStr", comparison) || functionName.Equals("MakeString", comparison))
        {
            if (args.Length != 2)
                return ThrowEvaluationException("MakeStr() takes exactly 2 arguments", function.Location);

            var size = LinqExpression.Variable(typeof(long), "size");
            return LinqExpression.Block(
                [size],
                LinqExpression.Assign(size, CallRuntime(nameof(LambdaRuntime.MakeSize), Box(args[0]), LinqExpression.Constant("String length"), LinqExpression.Constant("MakeStr"), functionNode)),
                CallRuntime(nameof(LambdaRuntime.MakeString), size, Box(args[1]), functionNode));
        }

        throw new MissingMethodException($"method not found: {functionName}");
    }

    public LinqExpression Visit(Identifier identifier, CancellationToken cancellationToken = default)
    {
        var identifierName = identifier.Name;

        if (_options.HasFlag(ExpressionOptions.UseLoops)
            && (identifierName.Equals("break", StringComparison.InvariantCultureIgnoreCase) || identifierName.Equals("continue", StringComparison.InvariantCultureIgnoreCase)))
            throw new NotSupportedException("Loops are not supported in lambda expressions.");

        // As in the evaluators, "null" is the null value with AllowNullParameter or UseTernaryLogic unless a parameter of that name is defined
        var isNullValue = (_options.HasFlag(ExpressionOptions.AllowNullParameter) || _options.HasFlag(ExpressionOptions.UseTernaryLogic))
            && identifierName.Equals("null", _options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

        if (_context is not null)
        {
            try
            {
                return LinqExpression.PropertyOrField(_context, identifierName);
            }
            catch (ArgumentException) when (isNullValue)
            {
                return NullConstant;
            }
        }

        var ignoreCase = _options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup);
        object? param = null;
        if (_parameters is not null &&
            (ignoreCase
                ? EvaluationHelper.GetParameterValueFromListNoCase(_parameters, identifierName, out param)
                : _parameters.TryGetValue(identifierName, out param)))
        {
            return param switch
            {
                // A parameter that is an expression is evaluated whenever the lambda runs
                Expression parameterExpression => CallRuntime(nameof(LambdaRuntime.EvaluateParameterExpression), LinqExpression.Constant(parameterExpression), _expressionContextConstant),
                AsyncExpression => throw new NCalcEvaluationException($"The value of the parameter '{identifierName}' is an AsyncExpression, which cannot be evaluated synchronously. Use an AsyncExpression for the outer expression or an Expression for the parameter.", identifier.Location),
                null => NullConstant,
                _ => LinqExpression.Constant(param)
            };
        }

        ExpressionParameter? dynamicParameter = null;
        if (ignoreCase
                ? EvaluationVisitor.GetParameterValueFromListNoCase(_expressionContext.DynamicParameters, identifierName, out dynamicParameter)
                : _expressionContext.DynamicParameters.TryGetValue(identifierName, out dynamicParameter))
        {
            return dynamicParameter is null
                ? NullConstant
                : CallRuntime(nameof(LambdaRuntime.EvaluateDynamicParameter), LinqExpression.Constant(dynamicParameter), LinqExpression.Constant(identifier), _expressionContextConstant);
        }

        if (isNullValue)
            return NullConstant;

        throw new NCalcParameterNotDefinedException(identifierName);
    }

    public LinqExpression Visit(LogicalExpressionList list, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var newList = LinqExpression.New(typeof(List<object?>));
        if (list.Count == 0)
            return newList;
        return LinqExpression.ListInit(newList, list.Select(e => Box(e.Accept(this, cancellationToken))));
    }

    public LinqExpression Visit(FunctionExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        throw new NotSupportedException("Function declarations are not supported in lambda expressions.");
    }

    public LinqExpression Visit(StatementSequence expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        List<LinqExpression> expressions = [];
        foreach (LogicalExpression expr in expression)
        {
            expressions.Add(Box(expr.Accept(this, cancellationToken)));
        }

        // As in the evaluators, the value is that of the last statement
        expressions[^1] = CallRuntime(nameof(LambdaRuntime.ValueOrNull), expressions[^1], _expressionContextConstant);
        return LinqExpression.Block(typeof(object), expressions);
    }

    /// <summary>
    /// Converts the compiled expression to the result type of the lambda.
    /// </summary>
    internal LinqExpression ConvertToResultType(LinqExpression body, Type resultType)
    {
        if (body.Type == resultType)
            return body;

        // A value whose type is known only when the lambda runs is converted as a cast converts it
        if (body.Type == typeof(object))
            return ConvertAtRuntime(body, resultType);

        // With OverflowProtection, a result that does not fit in the result type throws instead of being truncated
        return _options.HasFlag(ExpressionOptions.OverflowProtection)
            ? LinqExpression.ConvertChecked(body, resultType)
            : LinqExpression.Convert(body, resultType);
    }

    private LinqExpression ConvertAtRuntime(LinqExpression value, Type type)
    {
        var method = typeof(LambdaRuntime).GetMethod(nameof(LambdaRuntime.ConvertResult), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(type);
        return LinqExpression.Call(method, Box(value), _expressionContextConstant);
    }

    private LinqExpression Assignment(BinaryExpression expression, CancellationToken cancellationToken)
    {
        // Without a context object the parameters are compiled as constants, so an assignment could not change them
        if (_context is null)
            throw new NotSupportedException("Assignments are supported in lambda expressions only with a context object whose members are assigned to (ToLambda<TContext, TResult>).");

        if (expression.LeftExpression is not Identifier identifier)
            throw new NotSupportedException("Indexed access to parameters is not supported for Lambda expressions. Please submit an issue to https://github.com/Allied-Bits-Ltd/ncalc/issues if you need this support.");

        var member = LinqExpression.PropertyOrField(_context, identifier.Name);
        var node = LinqExpression.Constant(expression);
        LinqExpression newValue;

        if (expression.Type == BinaryExpressionType.Assignment)
        {
            newValue = CallRuntime(nameof(LambdaRuntime.ValueOrNull), Box(expression.RightExpression.Accept(this, cancellationToken)), _expressionContextConstant);
        }
        else
        {
            // As in the evaluators, a null value of the member gives null and leaves it unchanged
            var leftValue = LinqExpression.Variable(typeof(object), "left");
            var right = Box(expression.RightExpression.Accept(this, cancellationToken));
            newValue = LinqExpression.Block(
                [leftValue],
                LinqExpression.Assign(leftValue, Box(member)),
                LinqExpression.Condition(
                    CallRuntime(nameof(LambdaRuntime.IsNull), leftValue, _expressionContextConstant),
                    LinqExpression.Constant(LambdaRuntime.NoUpdate),
                    CallRuntime(nameof(LambdaRuntime.CompoundAssignment), leftValue, right, node, _expressionContextConstant)));
        }

        var value = LinqExpression.Variable(typeof(object), "value");
        return LinqExpression.Block(
            [value],
            LinqExpression.Assign(value, newValue),
            LinqExpression.Condition(
                LinqExpression.ReferenceEqual(value, LinqExpression.Constant(LambdaRuntime.NoUpdate)),
                NullConstant,
                LinqExpression.Block(
                    LinqExpression.IfThen(
                        CallRuntime(nameof(LambdaRuntime.ShouldUpdate), value, LinqExpression.Constant(identifier), _expressionContextConstant),
                        LinqExpression.Assign(member, ConvertAtRuntime(value, member.Type))),
                    value)));
    }

    private LinqExpression IfFunction(FunctionCall function, CancellationToken cancellationToken)
    {
        var parameters = function.Parameters;
        if (parameters.Count is < 2 or > 3)
            return ThrowEvaluationException("iff() takes 2 or 3 arguments", function.Location);

        // As in the evaluators, the third argument is optional, and the result is null when the condition is false
        var condition = CallRuntime(nameof(LambdaRuntime.ToBooleanRaw), Box(parameters[0].Accept(this, cancellationToken)), LinqExpression.Constant("iff"),
            NodeConstant(parameters[0]), _expressionContextConstant);
        var ifTrue = Box(parameters[1].Accept(this, cancellationToken));
        var ifFalse = parameters.Count == 3 ? Box(parameters[2].Accept(this, cancellationToken)) : NullConstant;
        return LinqExpression.Condition(condition, ifTrue, ifFalse);
    }

    private LinqExpression IfsFunction(FunctionCall function, CancellationToken cancellationToken)
    {
        var parameters = function.Parameters;
        if (parameters.Count < 2)
            return ThrowEvaluationException("ifs() takes at least 2 arguments", function.Location);

        var args = parameters.Select(p => Box(p.Accept(this, cancellationToken))).ToArray();

        // Pairs of a condition and a value, optionally followed by the default value; null when no condition is true
        LinqExpression result = args.Length % 2 == 1 ? args[^1] : NullConstant;
        for (int i = (args.Length / 2 - 1) * 2; i >= 0; i -= 2)
        {
            var condition = CallRuntime(nameof(LambdaRuntime.ToBooleanRaw), args[i], LinqExpression.Constant("ifs"), NodeConstant(parameters[i]), _expressionContextConstant);
            result = LinqExpression.Condition(condition, args[i + 1], result);
        }
        return result;
    }

    private LinqExpression InFunction(FunctionCall function, CancellationToken cancellationToken)
    {
        var parameters = function.Parameters;
        if (parameters.Count < 2)
            return ThrowEvaluationException("in() takes at least 2 arguments", function.Location);

        // The same equality as the "=" operator; the items are evaluated until one is equal
        var value = LinqExpression.Variable(typeof(object), "value");
        LinqExpression? found = null;
        for (var i = 1; i < parameters.Count; i++)
        {
            var item = CallRuntime(nameof(LambdaRuntime.InFunctionItem), value, Box(parameters[i].Accept(this, cancellationToken)), NodeConstant(parameters[i]), _expressionContextConstant);
            found = found is null ? item : LinqExpression.OrElse(found, item);
        }

        return LinqExpression.Block(
            [value],
            LinqExpression.Assign(value, Box(parameters[0].Accept(this, cancellationToken))),
            found!);
    }

    private LinqExpression PercentFunction(string method, LinqExpression[] args)
    {
        // The second argument is not evaluated when the first one is null
        var arg0 = LinqExpression.Variable(typeof(object), "arg0");
        var arg1 = LinqExpression.Variable(typeof(object), "arg1");
        return LinqExpression.Block(
            [arg0, arg1],
            LinqExpression.Assign(arg0, Box(args[0])),
            LinqExpression.Condition(
                LinqExpression.ReferenceEqual(arg0, NullConstant),
                NullConstant,
                LinqExpression.Block(
                    LinqExpression.Assign(arg1, Box(args[1])),
                    LinqExpression.Condition(
                        LinqExpression.ReferenceEqual(arg1, NullConstant),
                        NullConstant,
                        CallRuntime(method, arg0, arg1, _expressionContextConstant)))));
    }

    // An error that the evaluators report when the function is evaluated
    private static LinqExpression ThrowEvaluationException(string message, ExpressionLocation location)
    {
        var constructor = typeof(NCalcEvaluationException).GetConstructor([typeof(string), typeof(ExpressionLocation)])!;
        return LinqExpression.Throw(LinqExpression.New(constructor, LinqExpression.Constant(message), LinqExpression.Constant(location)), typeof(object));
    }

    private static LinqExpression Box(LinqExpression expression)
        => expression.Type == typeof(object) ? expression : LinqExpression.Convert(expression, typeof(object));

    private static LinqExpression NodeConstant(LogicalExpression expression)
        => LinqExpression.Constant(expression, typeof(LogicalExpression));

    private static Linq.MethodCallExpression CallRuntime(string name, params LinqExpression[] arguments)
        => LinqExpression.Call(typeof(LambdaRuntime).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!, arguments);

    private ExtendedMethodInfo? FindMethod(string methodName, LinqExpression[] methodArgs, StringComparison stringComparison)
    {
        if (_context is null)
            return null;

        var contextType = _context.Type;
        var objectType = typeof(object);

        do
        {
            var methods = contextType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name.Equals(methodName, stringComparison));

            var candidates = new List<ExtendedMethodInfo>();

            foreach (var potentialMethod in methods)
            {
                var methodParams = potentialMethod.GetParameters();

                // The value of an operator is known only when the lambda runs: it is converted to the type of the parameter then
                var conversions = 0;
                var arguments = methodArgs;
                if (methodParams.Length > 0)
                {
                    var hasParamsKeyword = methodParams[^1].IsDefined(typeof(ParamArrayAttribute));
                    arguments = new LinqExpression[methodArgs.Length];
                    for (var i = 0; i < methodArgs.Length; i++)
                    {
                        var parameterType = hasParamsKeyword && i >= methodParams.Length - 1
                            ? methodParams[^1].ParameterType.GetElementType()
                            : i < methodParams.Length ? methodParams[i].ParameterType : null;
                        if (methodArgs[i].Type == objectType && parameterType is not null && parameterType != objectType)
                        {
                            arguments[i] = ConvertAtRuntime(methodArgs[i], parameterType);
                            conversions++;
                        }
                        else
                        {
                            arguments[i] = methodArgs[i];
                        }
                    }
                }

                var preparedArguments = LinqUtils.PrepareMethodArgumentsIfValid(methodParams, arguments);

                if (preparedArguments != null)
                {
                    var candidate = new ExtendedMethodInfo
                    {
                        MethodInfo = potentialMethod,
                        PreparedArguments = preparedArguments.Item2,
                        Score = preparedArguments.Item1 + conversions * methodArgs.Length
                    };

                    if (candidate.Score == 0)
                        return candidate;

                    candidates.Add(candidate);
                }
            }

            if (candidates.Count != 0)
                return candidates.OrderBy(method => method.Score).First();

            contextType = contextType.BaseType;
        }
        while (contextType != null && contextType != objectType);

        return null;
    }

    public LinqExpression ConvertDoubleIfNoFraction(LinqExpression originalExpression)
    {
        if (!_options.HasFlag(ExpressionOptions.ReduceDivResultToInteger))
            return originalExpression;

        // Handle if the original expression is of a floating-point type
        if (originalExpression.Type == typeof(float) || originalExpression.Type == typeof(double) || originalExpression.Type == typeof(decimal))
        {
            // (floating)value
            var fpValue = LinqExpression.Convert(originalExpression, originalExpression.Type);
            // Math.Floor(value)
            MethodInfo? floorMethod = typeof(Math).GetMethod("Floor", new[] { originalExpression.Type });
            if (floorMethod is null)
            {
                return originalExpression;
            }
            var floorValue = LinqExpression.Call(floorMethod, fpValue);
            // value == Math.Floor(value)
            var noFraction = LinqExpression.Equal(fpValue, floorValue);
            // (long)value
            var longValue = LinqExpression.Convert(fpValue, typeof(long));
            // Box both to object for return type consistency
            var longAsObject = LinqExpression.Convert(longValue, typeof(object));
            var originalAsObject = LinqExpression.Convert(fpValue, typeof(object));
            // noFraction ? (object)(long)value : (object)value
            var condition = LinqExpression.Condition(noFraction, longAsObject, originalAsObject);

            return condition;
        }
        else
        {
            // If not double, just box it to object
            if (originalExpression.Type.IsValueType && originalExpression.Type != typeof(object))
            {
                return LinqExpression.Convert(originalExpression, typeof(object));
            }
            else
            {
                return originalExpression;
            }
        }
    }

    public static LinqExpression ConvertObjectExprToDouble(LinqExpression inputExpr)
    {
        // if (inputExpr is int) return (double)(int)inputExpr;
        var isInt = LinqExpression.TypeIs(inputExpr, typeof(int));
        var asInt = LinqExpression.Convert(LinqExpression.Convert(inputExpr, typeof(int)), typeof(double));

        // else if (inputExpr is long) return (double)(long)inputExpr;
        var isLong = LinqExpression.TypeIs(inputExpr, typeof(long));
        var asLong = LinqExpression.Convert(LinqExpression.Convert(inputExpr, typeof(long)), typeof(double));

        // else if (inputExpr is double) return (double)inputExpr;
        var isDouble = LinqExpression.TypeIs(inputExpr, typeof(double));
        var asDouble = LinqExpression.Convert(inputExpr, typeof(double));

        // else throw
        var throwExpr = LinqExpression.Throw(
            LinqExpression.New(typeof(InvalidCastException).GetConstructor(Type.EmptyTypes)!),
            typeof(double)
        );

        return LinqExpression.Condition(
            isInt, asInt,
            LinqExpression.Condition(
                isLong, asLong,
                LinqExpression.Condition(
                    isDouble, asDouble,
                    throwExpr
                )
            )
        );
    }

    public static LinqExpression BuildThrowNCalcFlowControl(LinqExpression returnValueExpr, ExpressionLocation location)
    {
        if (returnValueExpr is null) throw new ArgumentNullException(nameof(returnValueExpr));

        // Find ctor: NCalcFlowControl(object? returnValue, ExpressionLocation location)
        var ctor = typeof(NCalcFlowControl).GetConstructor(new[] { typeof(object), typeof(ExpressionLocation) })
                   ?? throw new InvalidOperationException("Matching constructor not found on NCalcFlowControl.");

        // Coerce/box return value to object
        var coercedReturn = returnValueExpr.Type == typeof(object)
            ? returnValueExpr
            : LinqExpression.Convert(returnValueExpr, typeof(object));

        // Location is a concrete value
        var locationConst = LinqExpression.Constant(location, typeof(ExpressionLocation));

        // new NCalcFlowControl(returnValue, location)
        var newException = LinqExpression.New(ctor, coercedReturn, locationConst);

        // throw new NCalcFlowControl(...)
        return LinqExpression.Throw(newException);
    }

    public LinqExpression LikeOperator(LinqExpression leftValue, LinqExpression? rightValue)
    {
        if (leftValue is null || rightValue is null)
            return LinqExpression.Constant(false);

        var leftObj = LinqExpression.Convert(leftValue, typeof(string));
        var rightObj = LinqExpression.Convert(rightValue, typeof(string));

        var objToString = typeof(object).GetMethod(nameof(object.ToString))!;
        var leftStr = LinqExpression.Call(leftObj, objToString);
        var rightStr = LinqExpression.Call(rightObj, objToString);

        // The same wildcard matcher as the evaluators use; a regular expression would backtrack exponentially on some patterns
        var miMatches = typeof(EvaluationHelper).GetMethod(nameof(EvaluationHelper.MatchesLikePattern), BindingFlags.NonPublic | BindingFlags.Static)!;
        var currentCulture = LinqExpression.Property(null, typeof(CultureInfo).GetProperty(nameof(CultureInfo.CurrentCulture))!);
        var callIsMatch = LinqExpression.Call(null, miMatches, leftStr, rightStr, LinqExpression.Constant(_caseInsensitiveStringComparer), currentCulture);

        // if either side is null should be false
        var leftNull = LinqExpression.Equal(leftObj, LinqExpression.Constant(null));
        var rightNull = LinqExpression.Equal(rightObj, LinqExpression.Constant(null));
        var anyNull = LinqExpression.OrElse(leftNull, rightNull);

        return LinqExpression.Condition(anyNull, LinqExpression.Constant(false), callIsMatch);
    }
}
