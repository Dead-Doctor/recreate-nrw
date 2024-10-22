using System.Collections.Concurrent;
using System.Reflection;
using JetBrains.Annotations;

namespace recreate_nrw.Util;

public static class Resources
{
    private static readonly Dictionary<string, object> Cache = new();
    private static readonly List<IDisposable> Disposables = new();

    [PublicAPI]
    public static T GetCached<T>(string path, Source source, Func<Stream, T> parser)
    {
        var key = $"{typeof(T)}:{path}";
        if (Cache.TryGetValue(key, out var cachedValue)) return (T) cachedValue;
        
        var value = Get(path, source, parser);
        Cache.Add(key, value!);
        return value;
    }

    [PublicAPI]
    public static T Get<T>(string path, Source source, Func<Stream, T> parser)
    {
        using var stream = source switch
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
    
    /// <summary>
    /// This does not remove the object from the cache!!!
    /// </summary>
    /// <param name="disposable">Object to dispose.</param>
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


public class AsyncResourceLoader<TId, TValue> where TId : notnull
{
    private readonly ConcurrentDictionary<TId, Task<TValue>> _loadingTasks = new();
    
    public Task<TValue> LoadResourceAsync(TId id, Func<TValue> loader)
    {
        return _loadingTasks.GetOrAdd(id, _ => Task.Run(() =>
        {
            var value = loader();
            _loadingTasks.TryRemove(id, out var _);
            return value;
        }));
    }
}
    
public class AsyncResourceLoaderCached<TId, TValue> where TId : notnull
{
    private readonly ConcurrentDictionary<TId, TValue> _cache = new();
    private readonly AsyncResourceLoader<TId, TValue> _resourceLoader = new();

    public async Task<TValue> Get(TId id, Func<TId, TValue> loader)
    {
        if (_cache.TryGetValue(id, out var cachedValue)) return cachedValue;

        return await _resourceLoader.LoadResourceAsync(id, () =>
        {
            var value = loader(id);
            if (!_cache.TryAdd(id, value)) Console.WriteLine($"[WARNING]: An already cached resource was loaded for a second time ({id}).");
            return value;
        });
    }
}