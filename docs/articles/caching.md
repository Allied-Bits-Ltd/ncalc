## Caching

When <xref:NCalc.Expression.Evaluate> is called on an expression, it is parsed once. If the same expression is reused the parse is not executed again. Thus, you can reuse <xref:NCalc.Expression>  instances by changing the parameters, and you will gain in performance because only the traversal of the expression tree will be done.

Moreover, each parsed expression is cached internally, which means you don't even have to care about reusing an <xref:NCalc.Expression> instance, the framework will do it for you.
The cache is automatically cleaned like the GC does when an Expression is no more used, or memory is needed (i.e. using [WeakReference<LogicalExpression>](https://learn.microsoft.com/en-us/dotnet/api/system.weakreference-1?view=net-8.0)).

The cache key consists of the expression text, the expression options, the culture and the advanced options, so the same text parsed with different settings (e.g. with and without `DecimalAsDefault`, or with another date order) is cached separately. Custom <xref:NCalc.Cache.ILogicalExpressionCache> implementations receive this composite key as the `expression` string. Relative date literals such as `#today#` are computed when the expression is evaluated, so a cached expression gives the current date.

The parsers themselves are also built once per combination of settings and reused, so parsing an expression that is not in the cache is fast too.

You can disable this behavior at the framework level by adding <xref:NCalc.ExpressionOptions.NoCache> at your [global expression context](expression_context.md).

```c#
MyContext.Value.Options = ExpressionOptions.NoCache;
```

You can also tell a specific <xref:NCalc.Expression> instance not to be taken from the cache.

```c#
var expression = new Expression("1 + 1", ExpressionOptions.NoCache);
```

You can customize the cache implementation using [Dependency Injection](dependency_injection.md).
