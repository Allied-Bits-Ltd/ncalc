using NCalc.Domain;
using NCalc.Helpers;

namespace NCalc.Visitors;

/// <summary>
/// Visitor dedicated to extract <see cref="Identifier"/> names from a <see cref="LogicalExpression"/>.
/// </summary>
public sealed class ParameterExtractionVisitor : ILogicalExpressionVisitor<List<string>>
{
    // Whether names are looked up ignoring case (LowerCaseIdentifierLookup), which decides the names that refer to user-function parameters
    private readonly bool _ignoreCase;

    public ParameterExtractionVisitor()
    {
    }

    /// <param name="ignoreCase">Whether the names are looked up ignoring case, as with <see cref="ExpressionOptions.LowerCaseIdentifierLookup"/>.</param>
    public ParameterExtractionVisitor(bool ignoreCase)
    {
        _ignoreCase = ignoreCase;
    }

    public List<string> Visit(Identifier identifier, CancellationToken cancellationToken = default) => [identifier.Name];

    public List<string> Visit(LogicalExpressionList list, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var parameters = new List<string>();
        foreach (var value in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value is Identifier identifier)
            {
                if (!parameters.Contains(identifier.Name))
                {
                    parameters.Add(identifier.Name);
                }
            }
            else
            {
                parameters.AddRange(value.Accept(this, cancellationToken));
            }
        }
        return parameters;
    }

    public List<string> Visit(UnaryExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        return expression.Expression.Accept(this, cancellationToken);
    }

    public List<string> Visit(BinaryExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var leftParameters = expression.LeftExpression.Accept(this, cancellationToken);
        var rightParameters = expression.RightExpression.Accept(this, cancellationToken);

        leftParameters.AddRange(rightParameters);
        return leftParameters.Distinct().ToList();
    }

    public List<string> Visit(TernaryExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var leftParameters = expression.LeftExpression.Accept(this, cancellationToken);
        var middleParameters = expression.MiddleExpression.Accept(this, cancellationToken);
        var rightParameters = expression.RightExpression.Accept(this, cancellationToken);

        leftParameters.AddRange(middleParameters);
        leftParameters.AddRange(rightParameters);
        return leftParameters.Distinct().ToList();
    }

    public List<string> Visit(FunctionCall function, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var parameters = new List<string>();

        var innerParameters = function.Parameters.Accept(this, cancellationToken);
        parameters.AddRange(innerParameters);

        return parameters.Distinct().ToList();
    }

    public List<string> Visit(PercentExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        return expression.Expression.Accept(this, cancellationToken);
    }

    public List<string> Visit(ImaginaryNumberExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        return expression.Expression.Accept(this, cancellationToken);
    }

    public List<string> Visit(VectorExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        return expression.Expressions.Accept(this, cancellationToken);
    }

    public List<string> Visit(ValueExpression expression, CancellationToken cancellationToken = default) => [];

    public List<string> Visit(ExpressionGroup group, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        return group.Expression.Accept(this, cancellationToken);
    }

    public List<string> Visit(FunctionExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        // The parameters of a user-defined function are its own names, not parameters of the expression. They are compared as the
        // evaluator looks them up: ordinally, or by their lower-case form with LowerCaseIdentifierLookup
        var function = expression.Function;
        var parameters = function.Body.Accept(this, cancellationToken);
        foreach (var parameter in function.Parameters)
        {
            var parameterName = _ignoreCase ? parameter.Name.ToLowerInvariant() : parameter.Name;
            parameters.RemoveAll(name => string.Equals(_ignoreCase ? name.ToLowerInvariant() : name, parameterName, StringComparison.Ordinal));
        }
        return parameters;
    }

    public List<string> Visit(StatementSequence seq, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        var parameters = new List<string>();
        foreach (var value in seq)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value is Identifier identifier)
            {
                if (!parameters.Contains(identifier.Name))
                {
                    parameters.Add(identifier.Name);
                }
            }
            else
            {
                parameters.AddRange(value.Accept(this, cancellationToken));
            }
        }
        return parameters;
    }
}