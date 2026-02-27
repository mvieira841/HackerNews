using System.Threading.RateLimiting;

namespace HackerNews.Api.Infrastructure.Interfaces;

public interface IRequestRateLimiter
{
    ValueTask<RateLimitLease> AcquireAsync(int permitCount = 1, CancellationToken cancellationToken = default);
}