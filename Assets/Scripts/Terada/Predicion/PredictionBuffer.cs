public class PredictionBuffer<T>
{
    readonly T[] buffer;

    public int Size => buffer.Length;

    public PredictionBuffer(int size)
    {
        buffer = new T[size];
    }

    public void Set(int tick, T value)
    {
        buffer[tick % buffer.Length] = value;
    }

    public T Get(int tick)
    {
        return buffer[tick % buffer.Length];
    }
}
