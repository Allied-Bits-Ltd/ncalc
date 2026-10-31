using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.DependencyInjection;
using NCalc.Cache;
using NCalc.Factories;

namespace NCalc.DependencyInjection;

public class NCalcServiceBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    public NCalcServiceBuilder WithExpressionFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TExpressionFactory>()
        where TExpressionFactory : class, IExpressionFactory
    {
        Services.ReplaceScoped<IExpressionFactory, TExpressionFactory>();
        return this;
    }

    public NCalcServiceBuilder WithAsyncExpressionFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAsyncExpressionFactory>()
        where TAsyncExpressionFactory : class, IAsyncExpressionFactory
    {
        Services.ReplaceScoped<IAsyncExpressionFactory, TAsyncExpressionFactory>();
        return this;
    }

    public NCalcServiceBuilder WithCache<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCache>() where TCache : class, ILogicalExpressionCache
    {
        Services.ReplaceSingleton<ILogicalExpressionCache, TCache>();
        return this;
    }

    public NCalcServiceBuilder WithLogicalExpressionFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TLogicalExpressionFactory>()
        where TLogicalExpressionFactory : class, ILogicalExpressionFactory
    {
        Services.ReplaceSingleton<ILogicalExpressionFactory, TLogicalExpressionFactory>();
        return this;
    }

    public NCalcServiceBuilder WithEvaluationVisitorFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TEvaluationVisitorFactory>()
        where TEvaluationVisitorFactory : class, IEvaluationVisitorFactory
    {
        Services.ReplaceTransient<IEvaluationVisitorFactory, TEvaluationVisitorFactory>();
        return this;
    }

    public NCalcServiceBuilder WithAsyncEvaluationVisitorFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAsyncEvaluationVisitorFactory>()
        where TAsyncEvaluationVisitorFactory : class, IAsyncEvaluationVisitorFactory
    {
        Services.ReplaceTransient<IAsyncEvaluationVisitorFactory, TAsyncEvaluationVisitorFactory>();
        return this;
    }
}