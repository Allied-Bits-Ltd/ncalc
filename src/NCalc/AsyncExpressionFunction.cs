namespace NCalc;

public delegate ValueTask<object?> AsyncExpressionFunction(AsyncExpressionFunctionData data, CancellationToken cancellationToken = default);