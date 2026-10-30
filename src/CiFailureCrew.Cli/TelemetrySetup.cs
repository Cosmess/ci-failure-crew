using CiFailureCrew.Core.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CiFailureCrew.Cli;

/// <summary>
/// Exports traces and metrics through OTLP when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set
/// (the Aspire AppHost sets it automatically). Disposing flushes pending data.
/// </summary>
internal sealed class TelemetrySetup : IDisposable
{
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;

    private TelemetrySetup(TracerProvider tracerProvider, MeterProvider meterProvider)
    {
        _tracerProvider = tracerProvider;
        _meterProvider = meterProvider;
    }

    public static TelemetrySetup? StartIfConfigured()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
        {
            return null;
        }

        // CreateDefault reads OTEL_SERVICE_NAME and OTEL_RESOURCE_ATTRIBUTES.
        var resource = ResourceBuilder.CreateDefault();
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")))
        {
            resource.AddService("ci-failure-crew");
        }

        var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource(CrewTelemetry.SourceName)
            .AddOtlpExporter()
            .Build();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resource)
            .AddMeter(CrewTelemetry.SourceName)
            .AddOtlpExporter()
            .Build();

        return new TelemetrySetup(tracerProvider, meterProvider);
    }

    public void Dispose()
    {
        _tracerProvider.Dispose();
        _meterProvider.Dispose();
    }
}
