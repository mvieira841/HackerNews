namespace HackerNews.Api.Infrastructure.Interfaces;

public interface ICacheService
{
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? absoluteExpirationRelativeToNow = null);
    T? Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan? absoluteExpirationRelativeToNow = null);
    void Remove(string key);
}
