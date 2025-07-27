namespace NCalc.Handlers;

public delegate ValueTask AsyncMatchStringHandler(MatchStringArgs args, CancellationToken cancellationToken = default);