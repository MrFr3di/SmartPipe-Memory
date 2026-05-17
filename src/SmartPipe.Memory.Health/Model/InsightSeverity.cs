namespace SmartPipe.Memory.Health;

/// <summary>Severity level of a generated insight.</summary>
public enum InsightSeverity
{
    /// <summary>Informational insight.</summary>
    Info,

    /// <summary>Warning insight.</summary>
    Warning,

    /// <summary>Critical insight requiring immediate attention.</summary>
    Critical,
}
