using System.Runtime.CompilerServices;

namespace SmartPipe.Memory.Infrastructure;

/// <summary>
/// A high‑performance bit array for tracking visited node indices in graph traversals.
/// Uses <see cref="ulong"/>[] and bitwise operations to provide O(1) access with minimal memory overhead.
/// </summary>
public sealed class FastBitArray
{
    private readonly ulong[] _bits;

    /// <summary>
    /// Initializes a new <see cref="FastBitArray"/> with the specified number of bits.
    /// </summary>
    /// <param name="size">The total number of bits. Must be positive.</param>
    public FastBitArray(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        _bits = new ulong[(size + 63) / 64];
    }

    /// <summary>
    /// Sets the bit at the specified index to 1.
    /// </summary>
    /// <param name="index">Zero‑based bit index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index)
    {
        _bits[index >> 6] |= 1UL << index;
    }

    /// <summary>
    /// Returns <c>true</c> if the bit at the specified index is set to 1.
    /// </summary>
    /// <param name="index">Zero‑based bit index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSet(int index)
    {
        return (_bits[index >> 6] & (1UL << index)) != 0;
    }

    /// <summary>
    /// Resets all bits to 0.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_bits, 0, _bits.Length);
    }
}
