namespace NCalc.Handlers;

public class AsyncFunctionArgs(Guid id, AsyncExpression[] parameters) : EventArgs
{
    public Guid Id { get; } = id;

    private object? _result;

    public object? Result
    {
        get => _result;
        set
        {
            _result = value;
            HasResult = true;
        }
    }

    public AsyncExpression[] Parameters { get; } = parameters;

    public bool HasResult { get; private set; }

    /// <summary>
    /// Evaluates the arguments. They are evaluated with the cancellation token of the evaluation that calls the function.
    /// </summary>
    public ValueTask<object?[]> EvaluateParametersAsync() => EvaluateParametersAsync(CancellationToken.None);

    /// <summary>
    /// Evaluates the arguments with the given cancellation token, or with the token of the evaluation that calls the function
    /// when the given one cannot be cancelled.
    /// </summary>
    public async ValueTask<object?[]> EvaluateParametersAsync(CancellationToken cancellationToken)
    {
        var values = new object?[Parameters.Length];
        for (var i = 0; i < values.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            values[i] = await Parameters[i].EvaluateAsync(cancellationToken).ConfigureAwait(false);
        }

        return values;
    }
}