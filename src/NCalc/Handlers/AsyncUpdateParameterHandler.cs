namespace NCalc.Handlers;

public delegate ValueTask AsyncUpdateParameterHandler(string name, UpdateParameterArgs args, CancellationToken cancellationToken = default);