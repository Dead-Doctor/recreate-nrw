using System.Reflection;

namespace recreate_nrw.Util;

public static class Resources
{
    private static readonly Dictionary<string, object> Cache = new();
    private static readonly List<IDisposable> Disposables = new();

    public static T GetCached<T>(string path, Source source, Func<Stream, T> parser)
    {
        var key = $"{typeof(T)}:{path}";
        if (Cache.TryGetValue(key, out var cachedValue)) return (T) cachedValue;
        
        var value = Get(path, source, parser);
        Cache.Add(key, value!);
        return value;
    }

    public static T Get<T>(string path, Source source, Func<Stream, T> parser)
    {
        var stream = source switch
        {
            Source.Memory => Stream.Null,
            Source.Embedded => Assembly.GetExecutingAssembly()
                                   .GetManifestResourceStream(typeof(Window), path.Replace('/', '.')) ??
                               throw new ArgumentException($"Could not find resource '{path}' in source '{source}'."),
            Source.WorkingDirectory => new FileStream(Path.Combine(Directory.GetCurrentDirectory(), path),
                FileMode.Open),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };

        var value = parser(stream);
        stream.Dispose();
        return value;
    }

    public static void RegisterDisposable(IDisposable disposable) => Disposables.Add(disposable);

    public static void DisposeAll()
    {
        foreach (var disposable in Disposables)
        {
            disposable.Dispose();
        }

        Disposables.Clear();
    }

    public static void Dispose(IDisposable disposable)
    {
        Disposables.Remove(disposable);
        disposable.Dispose();
    }
}

public enum Source
{
    Memory,
    Embedded,
    WorkingDirectory
}