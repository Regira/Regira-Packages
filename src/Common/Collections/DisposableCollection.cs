namespace Regira.Collections;

public class DisposableCollection<T> : List<T>, IDisposable
    where T : IDisposable
{
    public DisposableCollection()
    {
    }
    public DisposableCollection(int capacity)
        : base(capacity)
    {
    }
    public DisposableCollection(IEnumerable<T> items)
        : base(items)
    {
    }


    private void ReleaseUnmanagedResources()
    {
        foreach (var item in this)
        {
            item.Dispose();
        }
    }
    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }
    ~DisposableCollection()
    {
        ReleaseUnmanagedResources();
    }
}

public static class DisposableCollectionExtensions
{
    public static void Dispose<T>(this IEnumerable<T> items)
        where T : IDisposable
    {
        new DisposableCollection<T>(items).Dispose();
    }
}