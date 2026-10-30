using System.Text.Json.Serialization;

namespace CiFailureCrew.Core.Models;

/// <summary>What kind of failure made a job fail.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FailureCategory>))]
public enum FailureCategory
{
    /// <summary>The code does not compile, or a linter/analyzer rejected it.</summary>
    Compilation,

    /// <summary>A test ran and failed (assertion, exception, snapshot mismatch).</summary>
    TestFailure,

    /// <summary>A test or step that fails intermittently, unrelated to the change.</summary>
    Flaky,

    /// <summary>A package could not be restored, resolved or is incompatible.</summary>
    Dependency,

    /// <summary>The runner, network, disk or an external service failed.</summary>
    Infrastructure,

    /// <summary>The job or a step exceeded its time limit.</summary>
    Timeout,

    /// <summary>Missing or wrong configuration: secrets, environment variables, workflow settings.</summary>
    Configuration,

    Unknown
}
