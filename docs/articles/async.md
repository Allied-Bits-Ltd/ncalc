# Async Support

NCalc supports asynchronous evaluating an expression. Unlike popular belief, `async` is not faster than `sync`, but scales better.
It's recommended to use `async` if your expression is doing any CPU or IO bound work at some dynamic function or parameter, so your UI don't freeze or your web-server do can another things.
Learn more about `async` [in this article](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios).

In Allied Bits NCalc, asynchronous classes are included in the same package and assembly as the regular ones.

# Install
```shell
dotnet add package AlliedBits.NCalc
```

# Usage
```cs
var expression = new AsyncExpression("database_operation('SELECT FOO') == 'FOO'");

expression.Functions["database_operation"] = async (args) => {
    // My heavy database work.
    await Task.Delay(100);

    return "FOO";
};

var result = await expression.EvaluateAsync();
Debug.Assert(true.Equals(result));
```

<xref:NCalc.AsyncExpression> supports the same syntax, options, operators and built-in functions as <xref:NCalc.Expression> and gives the same results. The differences are:

* Custom functions (`Functions`), dynamic parameters (`DynamicParameters`) and the events (`EvaluateFunctionAsync`, `EvaluateParameterAsync`, `UpdateParameterAsync`, `MatchStringAsync`) are asynchronous; all handlers of an event are awaited.
* The static limits are separate: `AsyncExpression.MaxLoopIterations`, `AsyncExpression.MaxUserFunctionCallDepth` and `AsyncExpression.CheckExecutionStack` (see [Limits](options.md#limits)).
* `EvaluateAsync()` reports a parse error and an already cancelled token through the returned task instead of throwing from the call.
* A synchronous <xref:NCalc.Expression> can be the value of a parameter of an `AsyncExpression`; an `AsyncExpression` used as a parameter of a synchronous `Expression` throws <xref:NCalc.Exceptions.NCalcEvaluationException>.

`ToLambda()` is available only for the synchronous <xref:NCalc.Expression>.
