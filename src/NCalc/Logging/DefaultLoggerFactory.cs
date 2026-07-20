using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NCalc.Logging;

internal static class DefaultLoggerFactory
{
    public static ILoggerFactory Value { get; }

    static DefaultLoggerFactory()
    {
        bool enableConsole = AppContext.TryGetSwitch("NCalc.Logging.EnableConsole", out var console) && console;
        bool enableTrace = AppContext.TryGetSwitch("NCalc.Logging.EnableTrace", out var trace) && trace;

        // AOT-safe default: building a real LoggerFactory pulls in Microsoft.Extensions.Logging
        // dependency injection plus the configure lambda below. Mono full-AOT (e.g. iOS) cannot
        // AOT-compile that machinery and tries to JIT it at first use, which throws a fatal
        // ExecutionEngineException in aot-only mode. When no logging switch is enabled (the
        // common case, and always on AOT platforms), skip LoggerFactory.Create entirely and
        // return the null factory instead. This is behavior-equivalent to the previous
        // provider-less factory (both produce no log output) and works in the regular assembly
        // as well as the AOT one.
        if (!enableConsole && !enableTrace)
        {
            Value = NullLoggerFactory.Instance;
            return;
        }

        Value = LoggerFactory.Create(options =>
        {
            if (enableConsole)
            {
                options.AddConsole();
            }

            if (enableTrace)
            {
                options.AddTraceSource("NCalc");
            }
        });
    }
}