using System.Runtime.InteropServices;

namespace SmartPipe.Memory.Infrastructure;

/// <summary>
/// Padded 64‑bit atomic counter. The struct size is exactly 64 bytes (one cache line)
/// to prevent false sharing when used in parallel code paths.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct PaddedCounter64
{
    [FieldOffset(0)]
    private long _value;

    /// <summary>Returns the current counter value.</summary>
    public readonly long Value => _value;

    /// <summary>Initialises the counter with an optional start value.</summary>
    public PaddedCounter64(long initialValue = 0) => _value = initialValue;

    /// <summary>Atomically adds <paramref name="value"/> (default 1) and returns the new value.</summary>
    public long Add(long value = 1) => Interlocked.Add(ref _value, value);

    /// <summary>Atomically subtracts <paramref name="value"/> (default 1) and returns the new value.</summary>
    public long Subtract(long value = 1) => Interlocked.Add(ref _value, -value);

    /// <summary>Atomically sets the counter to <paramref name="value"/>.</summary>
    public void Reset(long value = 0) => Interlocked.Exchange(ref _value, value);
}

/// <summary>
/// Padded 32‑bit atomic counter. The struct size is exactly 64 bytes (one cache line)
/// to prevent false sharing when used in parallel code paths.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct PaddedCounter32
{
    [FieldOffset(0)]
    private int _value;

    /// <summary>Returns the current counter value.</summary>
    public readonly int Value => _value;

    /// <summary>Initialises the counter with an optional start value.</summary>
    public PaddedCounter32(int initialValue = 0) => _value = initialValue;

    /// <summary>Atomically adds <paramref name="value"/> (default 1) and returns the new value.</summary>
    public int Add(int value = 1) => Interlocked.Add(ref _value, value);

    /// <summary>Atomically subtracts <paramref name="value"/> (default 1) and returns the new value.</summary>
    public int Subtract(int value = 1) => Interlocked.Add(ref _value, -value);

    /// <summary>Atomically sets the counter to <paramref name="value"/>.</summary>
    public void Reset(int value = 0) => Interlocked.Exchange(ref _value, value);
}