using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;

namespace ValueCollections;

/// <summary>
/// Backing buffer for all ValueCollections
/// </summary>
/// <remarks>
/// You should dispose it after use to ensure the rented buffer is returned to the array pool.
/// </remarks>
internal readonly struct BackingBuffer<T> : IDisposable where T : struct, IDisposable
{
    public BackingBuffer()
    {
        Generation = (_BackingBuffer = InnerBackingBuffer<T>.Get()).Generation;
    }
    private readonly InnerBackingBuffer<T> _BackingBuffer;
    private readonly int Generation;
    private InnerBackingBuffer<T> SharedBackingBuffer
    {
        get
        {
            if (_BackingBuffer is null)
            {
                throw new NullReferenceException("Accessed a value collection that was never constructed");
            }
            else if (_BackingBuffer.Generation == Generation)
            {
                return _BackingBuffer;
            }
            else
            {
                throw new ObjectDisposedException("Accessed a value collection after it was disposed");
            }
        }
    }

    public ref T Data
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref SharedBackingBuffer.Data;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => SharedBackingBuffer.Dispose();
}

internal sealed class InnerBackingBuffer<T> : IDisposable where T : struct, IDisposable
{

    public InnerBackingBuffer() { }
    public T Data;
    public int Generation { get; private set; }
    public void Dispose()
    {
        Data.Dispose();
        Data = default;
        Generation++;
    }

    static readonly ObjectPool<InnerBackingBuffer<T>> Pool = new DefaultObjectPool<InnerBackingBuffer<T>>(
            new DefaultPooledObjectPolicy<InnerBackingBuffer<T>>(),
            maximumRetained: 3000
        );

    public static InnerBackingBuffer<T> Get() => Pool.Get();
}