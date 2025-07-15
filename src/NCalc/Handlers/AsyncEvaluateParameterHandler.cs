namespace NCalc.Handlers;

public delegate ValueTask AsyncEvaluateParameterHandler(string name, AsyncParameterArgs args, CancellationToken cancellationToken = default);