namespace SmartPipe.Memory.Storage;

/// <summary>
/// Represents the current schema version of the graph store.
/// Used for migration checks between library versions.
/// </summary>
/// <param name="Major">Major version number.</param>
/// <param name="Minor">Minor version number.</param>
public readonly record struct SchemaVersion(int Major, int Minor)
{
    /// <summary>
    /// Current schema version.
    /// </summary>
    public static SchemaVersion Current => new(0, 1);
}