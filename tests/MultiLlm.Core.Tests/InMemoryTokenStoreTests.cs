using MultiLlm.Core.Auth;

namespace MultiLlm.Core.Tests;

public class InMemoryTokenStoreTests
{
    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsStoredToken()
    {
        var store = new InMemoryTokenStore();
        var token = new AuthToken("access-token", DateTimeOffset.UtcNow.AddMinutes(10), "refresh-token");

        await store.SetAsync("codex/dev", token);
        var result = await store.GetAsync("codex/dev");

        Assert.Equal(token, result);
    }

    [Fact]
    public async Task Inject_ManualTokenLoad_ReturnsStoredToken()
    {
        var store = new InMemoryTokenStore();
        var token = new AuthToken("manual-token", DateTimeOffset.UtcNow.AddMinutes(5));

        store.Inject("codex/manual", token);
        var result = await store.GetAsync("codex/manual");

        Assert.Equal(token, result);
    }

    [Fact]
    public async Task GetAsync_RemovesAndReturnsNull_ForExpiredToken()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new FakeTimeProvider(now);
        var store = new InMemoryTokenStore(timeProvider: timeProvider);

        await store.SetAsync("codex/expired", new AuthToken("expired", now.AddMinutes(1)));
        timeProvider.Advance(TimeSpan.FromMinutes(2));

        var result = await store.GetAsync("codex/expired");

        Assert.Null(result);

        var secondRead = await store.GetAsync("codex/expired");
        Assert.Null(secondRead);
    }

    private sealed class FakeTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan by) => _utcNow = _utcNow.Add(by);
    }
}
