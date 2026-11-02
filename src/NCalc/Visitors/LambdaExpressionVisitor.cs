using System.Reflection;
using System.Runtime.CompilerServices;
using ExtendedNumerics;

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

    private int _depth;

    /// <summary>
    /// The greatest depth of nesting of the visited expression, which tells how deeply the compiler of the lambda recurses.
    /// </summary>
    internal int MaxDepth { get; private set; }

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
        var conditional = Box(VisitChild(expression.LeftExpression, cancellationToken));
        var ifTrue = Box(VisitChild(expression.MiddleExpression, cancellationToken));
        var ifFalse = Box(VisitChild(expression.RightExpression, cancellationToken));

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

        // A chain of operators such as 1 + 2 + 3 + ... is a left-deep tree; it is compiled as a sequence of steps instead of nested
        // blocks, so that a long chain does not make FastExpressionCompiler recurse deeply (NCALC-202)
        var left = expression.LeftExpression is BinaryExpression leftBinary && IsChainable(leftBinary.Type)
            ? CompileChain(leftBinary, cancellationToken)
            : Box(VisitChild(expression.LeftExpression, cancellationToken));
        var right = Box(VisitChild(expression.RightExpression, cancellationToken));
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

    // The binary operators that compute a value from their operands (not assignments, loops or indexes)
    private static bool IsChainable(BinaryExpressionType type)
        => type is not (BinaryExpressionType.Assignment or BinaryExpressionType.PlusAssignment or BinaryExpressionType.MinusAssignment
            or BinaryExpressionType.MultiplyAssignment or BinaryExpressionType.DivAssignment or BinaryExpressionType.AndAssignment
            or BinaryExpressionType.OrAssignment or BinaryExpressionType.XOrAssignment or BinaryExpressionType.WhileLoop
            or BinaryExpressionType.IndexAccess or BinaryExpressionType.RangeIndex or BinaryExpressionType.Unknown);

    // Compiles a left-deep chain of binary operators as steps on one variable, with the same order of evaluation and the same
    // treatment of null as nested operators: value = first operand; value = value op1 operand1; value = value op2 operand2; ...
    private LinqExpression CompileChain(BinaryExpression top, CancellationToken cancellationToken)
    {
        if (++_depth > MaxDepth)
            MaxDepth = _depth;
        try
        {
            EvaluationHelper.EnsureSufficientExecutionStackToVisit();

            var nodes = new List<BinaryExpression>();
            LogicalExpression current = top;
            while (current is BinaryExpression binary && IsChainable(binary.Type))
            {
                nodes.Add(binary);
                current = binary.LeftExpression;
            }

            nodes.Reverse();

            var value = LinqExpression.Variable(typeof(object), "chain");
            var decided = LinqExpression.Variable(typeof(object), "decided");
            var statements = new List<LinqExpression> { LinqExpression.Assign(value, Box(VisitChild(current, cancellationToken))) };

            foreach (var node in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nodeConstant = LinqExpression.Constant(node);
                var right = Box(VisitChild(node.RightExpression, cancellationToken));

                switch (node.Type)
                {
                    case BinaryExpressionType.Equal:
                    case BinaryExpressionType.NotEqual:
                    case BinaryExpressionType.Greater:
                    case BinaryExpressionType.GreaterOrEqual:
                    case BinaryExpressionType.Less:
                    case BinaryExpressionType.LessOrEqual:
                        statements.Add(LinqExpression.Assign(value, Box(CallRuntime(nameof(LambdaRuntime.Compare), value, right, nodeConstant, _expressionContextConstant))));
                        break;

                    case BinaryExpressionType.And:
                    case BinaryExpressionType.Or:
                    case BinaryExpressionType.XOr:
                        statements.Add(LinqExpression.Assign(decided, CallRuntime(nameof(LambdaRuntime.LogicalLeft), value, nodeConstant, _expressionContextConstant)));
                        statements.Add(LinqExpression.Assign(value, LinqExpression.Condition(
                            LinqExpression.ReferenceEqual(decided, LinqExpression.Constant(LambdaRuntime.Undecided)),
                            CallRuntime(nameof(LambdaRuntime.LogicalRight), value, right, nodeConstant, _expressionContextConstant),
                            decided)));
                        break;

                    default:
                        statements.Add(LinqExpression.Assign(value, LinqExpression.Condition(
                            CallRuntime(nameof(LambdaRuntime.IsNull), value, _expressionContextConstant),
                            NullConstant,
                            CallRuntime(nameof(LambdaRuntime.Binary), value, right, nodeConstant, _expressionContextConstant))));
                        break;
                }
            }

            statements.Add(value);
            return LinqExpression.Block(typeof(object), [value, decided], statements);
        }
        finally
        {
            _depth--;
        }
    }

    public LinqExpression Visit(UnaryExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        if (expression.Type == UnaryExpressionType.Return)
            throw new NotSupportedException("The return statement is not supported in lambda expressions.");

        var operand = Box(VisitChild(expression.Expression, cancellationToken));
        return CallRuntime(nameof(LambdaRuntime.Unary), operand, LinqExpression.Constant(expression), _expressionContextConstant);
    }

    public LinqExpression Visit(PercentExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var value = Box(VisitChild(expression.Expression, cancellationToken));
        return CallRuntime(nameof(LambdaRuntime.Percent), value, _expressionContextConstant);
    }

    public LinqExpression Visit(ImaginaryNumberExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var value = Box(VisitChild(expression.Expression, cancellationToken));
        return CallRuntime(nameof(LambdaRuntime.Imaginary), value, LinqExpression.Constant(expression), _expressionContextConstant);
    }

    public LinqExpression Visit(VectorExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var components = LinqExpression.NewArrayInit(typeof(object), expression.Expressions.Select(e => Box(VisitChild(e, cancellationToken))));
        return CallRuntime(nameof(LambdaRuntime.Vector), components, LinqExpression.Constant(expression));
    }

    public LinqExpression Visit(ExpressionGroup group, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        return VisitChild(group.Expression, cancellationToken);
    }

    public LinqExpression Visit(ValueExpression expression, CancellationToken cancellationToken = default)
    {
        // A relative date or time literal (#now#) is computed whenever the compiled lambda runs
        if (expression.ValueFactory is { } valueFactory)
            return LinqExpression.Convert(LinqExpression.Invoke(LinqExpression.Constant(valueFactory)), expression.Value!.GetType());

        return expression.Value is null ? LinqExpression.Constant(null) : ValueConstant(expression.Value);
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

        // A wrong number of arguments of a built-in function is reported before the arguments are visited, as the evaluators do
        // (Pow(null) said that the parameter null is not defined; NCALC-331), unless the context has a method of that name
        var contextComparison = ignoreIdentifierCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture;
        if (GetArgumentCountError(functionName, parameters.Count, comparison) is { } countError && !HasContextMethod(functionName, contextComparison))
            return ThrowEvaluationException(countError, function.Location);

        var args = new LinqExpression[parameters.Count];
        for (var i = 0; i < parameters.Count; i++)
        {
            args[i] = VisitChild(parameters[i], cancellationToken);
        }

        //Context methods take precedence over built-in functions because they're user-customizable.
        var contextCall = CallContextMethod(functionName, args, contextComparison, function);
        if (contextCall != null)
            return contextCall;

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
                _ => ValueConstant(param)
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
        return LinqExpression.ListInit(newList, list.Select(e => Box(VisitChild(e, cancellationToken))));
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
            expressions.Add(Box(VisitChild(expr, cancellationToken)));
        }

        // The values of the other statements are discarded by a call: as statements of the block, a list or in() made
        // FastExpressionCompiler emit invalid IL (NCALC-210)
        for (var i = 0; i < expressions.Count - 1; i++)
        {
            expressions[i] = CallRuntime(nameof(LambdaRuntime.Discard), expressions[i]);
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

        // A value whose type is known only when the lambda runs is converted as a cast converts it; so is a BigDecimal, whose explicit
        // conversions depend on the current culture or do not exist for some types (NCALC-320)
        if (body.Type == typeof(object) || body.Type == typeof(BigDecimal))
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
            newValue = CallRuntime(nameof(LambdaRuntime.ValueOrNull), Box(VisitChild(expression.RightExpression, cancellationToken)), _expressionContextConstant);
        }
        else
        {
            // As in the evaluators, a null value of the member gives null and leaves it unchanged
            var leftValue = LinqExpression.Variable(typeof(object), "left");
            var right = Box(VisitChild(expression.RightExpression, cancellationToken));
            newValue = LinqExpression.Block(
                [leftValue],
                LinqExpression.Assign(leftValue, Box(member)),
                LinqExpression.Condition(
                    CallRuntime(nameof(LambdaRuntime.IsNull), leftValue, _expressionContextConstant),
                    LinqExpression.Constant(LambdaRuntime.NoUpdate),
                    CallRuntime(nameof(LambdaRuntime.CompoundAssignment), leftValue, right, node, _expressionContextConstant)));
        }

        // The value is converted to the type of the member without loss (2.5 is not stored in an int member), and the value
        // of the assignment is the value stored (NCALC-203)
        var value = LinqExpression.Variable(typeof(object), "value");
        var stored = LinqExpression.Variable(member.Type, "stored");
        var convert = typeof(LambdaRuntime).GetMethod(nameof(LambdaRuntime.ConvertAssignedValue), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(member.Type);
        return LinqExpression.Block(
            [value],
            LinqExpression.Assign(value, newValue),
            LinqExpression.Condition(
                LinqExpression.ReferenceEqual(value, LinqExpression.Constant(LambdaRuntime.NoUpdate)),
                NullConstant,
                LinqExpression.Condition(
                    CallRuntime(nameof(LambdaRuntime.ShouldUpdate), value, LinqExpression.Constant(identifier), _expressionContextConstant),
                    LinqExpression.Block(
                        [stored],
                        LinqExpression.Assign(stored, LinqExpression.Call(convert, value, LinqExpression.Constant(identifier))),
                        LinqExpression.Assign(member, stored),
                        Box(stored)),
                    value)));
    }

    private LinqExpression IfFunction(FunctionCall function, CancellationToken cancellationToken)
    {
        var parameters = function.Parameters;
        if (parameters.Count is < 2 or > 3)
            return ThrowEvaluationException("iff() takes 2 or 3 arguments", function.Location);

        // As in the evaluators, the third argument is optional, and the result is null when the condition is false
        var condition = CallRuntime(nameof(LambdaRuntime.ToBooleanRaw), Box(VisitChild(parameters[0], cancellationToken)), LinqExpression.Constant("iff"),
            NodeConstant(parameters[0]), _expressionContextConstant);
        var ifTrue = Box(VisitChild(parameters[1], cancellationToken));
        var ifFalse = parameters.Count == 3 ? Box(VisitChild(parameters[2], cancellationToken)) : NullConstant;
        return LinqExpression.Condition(condition, ifTrue, ifFalse);
    }

    private LinqExpression IfsFunction(FunctionCall function, CancellationToken cancellationToken)
    {
        var parameters = function.Parameters;
        if (parameters.Count < 2)
            return ThrowEvaluationException("ifs() takes at least 2 arguments", function.Location);

        var args = parameters.Select(p => Box(VisitChild(p, cancellationToken))).ToArray();

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
            var item = CallRuntime(nameof(LambdaRuntime.InFunctionItem), value, Box(VisitChild(parameters[i], cancellationToken)), NodeConstant(parameters[i]), _expressionContextConstant);
            found = found is null ? item : LinqExpression.OrElse(found, item);
        }

        return LinqExpression.Block(
            [value],
            LinqExpression.Assign(value, Box(VisitChild(parameters[0], cancellationToken))),
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

    private LinqExpression VisitChild(LogicalExpression expression, CancellationToken cancellationToken)
    {
        if (++_depth > MaxDepth)
            MaxDepth = _depth;
        try
        {
            return expression.Accept(this, cancellationToken);
        }
        finally
        {
            _depth--;
        }
    }

    // A constant value. A decimal is read from a field when the lambda runs, because FastExpressionCompiler emits decimal constants
    // (also boxed ones) without their scale, so 2.0 became 2 (NCALC-241).
    private static LinqExpression ValueConstant(object value)
        => value is decimal m
            ? LinqExpression.Field(LinqExpression.Constant(new StrongBox<decimal>(m)), nameof(StrongBox<decimal>.Value))
            : LinqExpression.Constant(value);

    private static LinqExpression Box(LinqExpression expression)
        => expression.Type == typeof(object) ? expression : LinqExpression.Convert(expression, typeof(object));

    private static LinqExpression NodeConstant(LogicalExpression expression)
        => LinqExpression.Constant(expression, typeof(LogicalExpression));

    private static Linq.MethodCallExpression CallRuntime(string name, params LinqExpression[] arguments)
        => LinqExpression.Call(typeof(LambdaRuntime).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!, arguments);

    // The message of the evaluators for a built-in function with a fixed number of arguments that is called with another number,
    // or null
    private static string? GetArgumentCountError(string functionName, int count, StringComparison comparison)
    {
        if (functionName.Equals("%", StringComparison.Ordinal) || functionName.Equals("PercentOf", comparison))
            return count == 2 ? null : "PercentOf() takes exactly 2 arguments";
        if (functionName.Equals("PercentDiff", comparison))
            return count == 2 ? null : "PercentDiff() takes exactly 2 arguments";
        foreach (var unaryFunction in LambdaRuntime.UnaryFunctions)
        {
            if (functionName.Equals(unaryFunction.Key, comparison))
                return count == 1 ? null : $"{(unaryFunction.Key == "Trunc" ? "Truncate" : unaryFunction.Key)}() takes exactly 1 argument";
        }
        foreach (var binaryFunction in LambdaRuntime.BinaryFunctions)
        {
            if (functionName.Equals(binaryFunction.Key, comparison))
                return count == 2 ? null : $"{binaryFunction.Key}() takes exactly 2 arguments";
        }
        if (functionName.Equals("MakeList", comparison))
            return count is 1 or 2 ? null : "MakeList() takes 1 or 2 arguments";
        if (functionName.Equals("MakeStr", comparison) || functionName.Equals("MakeString", comparison))
            return count == 2 ? null : "MakeStr() takes exactly 2 arguments";
        return null;
    }

    private bool HasContextMethod(string methodName, StringComparison stringComparison)
        => _context is not null
            && _context.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(m => m.Name.Equals(methodName, stringComparison));

    // A call of a method of the context object, or null when the context has no method of that name that can take the arguments.
    // The overload is chosen by one rule (LambdaRuntime.BindContextMethod), whatever the form of the arguments (NCALC-314): when the
    // lambda is compiled if the types of the arguments decide it, otherwise when it runs, by the types of the values, which are
    // converted without loss (NCALC-203). The methods of the base classes are candidates too (NCALC-317).
    private LinqExpression? CallContextMethod(string methodName, LinqExpression[] args, StringComparison stringComparison, FunctionCall function)
    {
        if (_context is null)
            return null;

        var candidates = new List<ContextMethodCandidate>();
        var level = 0;
        for (var contextType = _context.Type; contextType != null && contextType != typeof(object); contextType = contextType.BaseType)
        {
            var depth = level++;
            candidates.AddRange(contextType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name.Equals(methodName, stringComparison) && !m.ContainsGenericParameters && CanBindWhenRunning(m.GetParameters(), args))
                .Select(m => new ContextMethodCandidate(m, depth)));
        }

        if (candidates.Count == 0)
            return null;

        return ChooseWhenCompiling(candidates, args) is { } method
            ? CallWhenCompiled(method, args)
            : CallBoundWhenRunning(candidates.ToArray(), args, function);
    }

    // The overload that LambdaRuntime.BindContextMethod chooses for any values of the arguments, or null when the choice depends on the values
    private static ContextMethodCandidate? ChooseWhenCompiling(List<ContextMethodCandidate> candidates, LinqExpression[] args)
    {
        // A value of a non-nullable value type has the type of the argument; a value of another type can have a more derived type
        // or be null, and so change the costs of the conversions
        if (candidates.Count != 1 && !args.All(a => a.Type.IsValueType && Nullable.GetUnderlyingType(a.Type) is null))
            return null;

        ContextMethodCandidate? best = null;
        var bestCost = 0;
        foreach (var candidate in candidates)
        {
            var cost = GetConversionCost(candidate, args, out var dependsOnValues);
            if (!dependsOnValues && (best is null || LambdaRuntime.IsPreferred(candidate, cost, best, bestCost, args.Length)))
            {
                best = candidate;
                bestCost = cost;
            }
        }

        if (best is null)
            return null;

        // An overload whose arguments are converted only for some values is chosen for them when it is preferred, so then the
        // choice is left to the values
        foreach (var candidate in candidates)
        {
            var cost = GetConversionCost(candidate, args, out var dependsOnValues);
            if (dependsOnValues && LambdaRuntime.IsPreferred(candidate, cost, best, bestCost, args.Length))
                return null;
        }

        return best;
    }

    // The total cost of converting the arguments to the parameters of an overload, as LambdaRuntime.BindContextMethod computes it
    private static int GetConversionCost(ContextMethodCandidate candidate, LinqExpression[] args, out bool dependsOnValues)
    {
        dependsOnValues = false;
        var total = candidate.ParamsElementType is null ? 0 : LambdaRuntime.ParamsCost;
        for (var i = 0; i < args.Length; i++)
        {
            var cost = LambdaRuntime.GetConversionCost(args[i].Type, candidate.GetArgumentType(i));
            if (cost is < 0 or LambdaRuntime.LosslessCost)
                dependsOnValues = true;
            total += Math.Max(cost, 0);
        }

        return total;
    }

    private Linq.MethodCallExpression CallWhenCompiled(ContextMethodCandidate method, LinqExpression[] args)
    {
        static LinqExpression ConvertTo(LinqExpression argument, Type type)
            => argument.Type == type ? argument : LinqExpression.Convert(argument, type);

        var fixedCount = method.FixedParameterCount;
        var arguments = new LinqExpression[method.ParameterTypes.Length];
        for (var i = 0; i < fixedCount; i++)
        {
            arguments[i] = ConvertTo(args[i], method.ParameterTypes[i]);
        }

        if (method.ParamsElementType is { } elementType)
            arguments[fixedCount] = LinqExpression.NewArrayInit(elementType, args.Skip(fixedCount).Select(a => ConvertTo(a, elementType)));

        return LinqExpression.Call(_context!, method.Method, arguments);
    }

    // Whether the method can take arguments of these types: an argument of type object can have a value of any type
    private static bool CanBindWhenRunning(ParameterInfo[] parameters, LinqExpression[] args)
    {
        var hasParams = parameters.Length > 0 && parameters[^1].IsDefined(typeof(ParamArrayAttribute));
        var fixedCount = hasParams ? parameters.Length - 1 : parameters.Length;
        if (hasParams ? args.Length < fixedCount : args.Length != fixedCount)
            return false;

        for (var i = 0; i < args.Length; i++)
        {
            var parameterType = i < fixedCount ? parameters[i].ParameterType : parameters[^1].ParameterType.GetElementType()!;
            if (parameterType.IsByRef || parameterType.IsPointer || !CanConvertWhenRunning(args[i].Type, parameterType))
                return false;
        }

        return true;
    }

    private static bool CanConvertWhenRunning(Type from, Type to)
    {
        // A number may be converted to another numeric type, or an enum, without loss
        if (from == typeof(object) || LambdaRuntime.GetConversionCost(from, to) >= 0)
            return true;
        return Nullable.GetUnderlyingType(from) is { } underlyingType && CanConvertWhenRunning(underlyingType, to);
    }

    // Chooses the overload by the values of the arguments and calls it:
    // arguments = [args..., null]; overload = BindContextMethod(arguments, call); overload == 0 ? ctx.M0((T)arguments[0], ...) : ...
    private LinqExpression CallBoundWhenRunning(ContextMethodCandidate[] candidates, LinqExpression[] args, FunctionCall function)
    {
        var call = new ContextMethodCall(function.Identifier.Name, candidates, function);
        var arguments = LinqExpression.Variable(typeof(object[]), "arguments");
        var overload = LinqExpression.Variable(typeof(int), "overload");

        LinqExpression? result = null;
        for (var k = candidates.Length - 1; k >= 0; k--)
        {
            var candidate = candidates[k];
            var callArguments = candidate.ParameterTypes.Select((type, i) =>
                (LinqExpression)LinqExpression.Convert(LinqExpression.ArrayIndex(arguments, LinqExpression.Constant(i)), type));
            LinqExpression invoke = LinqExpression.Call(_context, candidate.Method, callArguments);
            invoke = invoke.Type == typeof(void) ? LinqExpression.Block(invoke, NullConstant) : Box(invoke);
            result = result is null ? invoke : LinqExpression.Condition(LinqExpression.Equal(overload, LinqExpression.Constant(k)), invoke, result);
        }

        return LinqExpression.Block(
            typeof(object),
            [arguments, overload],
            LinqExpression.Assign(arguments, LinqExpression.NewArrayInit(typeof(object), args.Select(Box).Append(NullConstant))),
            LinqExpression.Assign(overload, CallRuntime(nameof(LambdaRuntime.BindContextMethod), arguments, LinqExpression.Constant(call))),
            result!);
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
