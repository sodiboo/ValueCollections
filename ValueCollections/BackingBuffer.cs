using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

namespace ValueCollections;

/// <summary>
/// Performance counters for the pooling behaviours of the backing buffers.
/// </summary>
public static class ValueBufferCounters {
    /// <summary>
    /// Number of buffers that are allocated at the moment.
    /// </summary>
    public static int Active => _active;
    private static int _active;
    internal static void Constructed() => Interlocked.Increment(ref _active);
    internal static void Finalized() => Interlocked.Decrement(ref _active);

    /// <summary>
    /// Number of buffers that are currently leased out (actively being used and haven't been disposed yet)
    /// </summary>
    public static int Leased => _leased;
    private static int _leased;
    internal static void Acquired() => Interlocked.Increment(ref _leased);
    internal static void Returned() => Interlocked.Decrement(ref _leased);

    /// <summary>
    /// Number of buffers that were garbage collected; they were never disposed.
    /// </summary>
    public static int Leaked => _leaked;
    private static int _leaked;
    internal static void Leak() => Interlocked.Increment(ref _leaked);
}

/// <summary>
/// Backing buffer for all ValueCollections
/// </summary>
/// <remarks>
/// You should dispose it after use to ensure the rented buffer is returned to the array pool.
/// </remarks>
internal readonly struct BackingBuffer<T> : IDisposable where T : struct, IDisposable {
    public BackingBuffer() {
        Generation = (_BackingBuffer = InnerBackingBuffer<T>.Get()).Generation;
    }
    private readonly InnerBackingBuffer<T> _BackingBuffer;
    private readonly int Generation;
    private InnerBackingBuffer<T> SharedBackingBuffer {
        get {
            if (_BackingBuffer is null) {
                throw new NullReferenceException("Accessed a value collection that was never constructed");
            }
            else if (_BackingBuffer.Generation == Generation) {
                return _BackingBuffer;
            }
            else {
                throw new ObjectDisposedException("Accessed a value collection after it was disposed");
            }
        }
    }

    public ref T Data {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref SharedBackingBuffer.Data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => SharedBackingBuffer.Dispose();
}

internal sealed class InnerBackingBuffer<T> : IDisposable where T : struct, IDisposable {
    private bool Active { get; set; }
    private T _data;
    public ref T Data {
        get {
            if (Active) {
                return ref _data;
            }
            else {
                throw new ObjectDisposedException("unreachable");
            }
        }
    }
    public int Generation { get; private set; }

    static readonly ObjectPool<InnerBackingBuffer<T>> Pool = new DefaultObjectPool<InnerBackingBuffer<T>>(
            new DefaultPooledObjectPolicy<InnerBackingBuffer<T>>(),
            maximumRetained: 3000
        );

    public InnerBackingBuffer() => ValueBufferCounters.Constructed();

    public static InnerBackingBuffer<T> Get() {
        ValueBufferCounters.Acquired();
        var self = Pool.Get();
        self.Active = true;
        return self;
    }

    public void Dispose() {
        try {
            Data.Dispose();
        }
        finally {
            Data = default;
            Active = false;
            Generation++;
            Pool.Return(this);
            ValueBufferCounters.Returned();
        }
    }

    ~InnerBackingBuffer() {
        if (Active) {
            ValueBufferCounters.Returned();
        }
        ValueBufferCounters.Leak();
        ValueBufferCounters.Finalized();
    }
}