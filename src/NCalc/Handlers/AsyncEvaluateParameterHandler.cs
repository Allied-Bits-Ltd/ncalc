namespace NCalc.Handlers;

public delegate ValueTask AsyncEvaluateParameterHandler(string name, ParameterArgs args, CancellationToken cancellationToken = default);