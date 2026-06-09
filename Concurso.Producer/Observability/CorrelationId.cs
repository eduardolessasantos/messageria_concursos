using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Concurso.Producer.Observability;

/// <summary>
/// Utility to create a logging scope with a CorrelationId and an Activity for distributed tracing.
/// Returns IDisposable that disposes Activity and scope.
/// </summary>
public static class CorrelationId
{
    private static readonly ActivitySource ActivitySource = new("Concurso.Producer");

    public static IDisposable BeginScope(ILogger logger, string? correlationId = null)
    {
        var id = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId!;
        var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = id
        });

        var activity = ActivitySource.StartActivity("operation", ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("correlationId", id);
        }

        return new CompositeScope(scope, activity);
    }

    private sealed class CompositeScope : IDisposable
    {
        private readonly IDisposable _scope;
        private readonly Activity? _activity;

        public CompositeScope(IDisposable scope, Activity? activity)
        {
            _scope = scope;
            _activity = activity;
        }

        public void Dispose()
        {
            try
            {
                _activity?.Stop();
            }
            catch { }

            _scope.Dispose();
        }
    }
}
