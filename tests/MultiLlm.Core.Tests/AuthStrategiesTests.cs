using MultiLlm.Core.Auth;

namespace MultiLlm.Core.Tests;

public class AuthStrategiesTests
{
    [Fact]
    public async Task NoAuth_DoesNotModifyHeaders()
    {
        var strategy = new NoAuth();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/v1/chat");

        await strategy.ApplyAsync(request);

        Assert.Empty(request.Headers);
    }

    [Fact]
    public async Task ApiKeyAuth_SetsDefaultHeader()
    {
        var strategy = new ApiKeyAuth("secret");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test");

        await strategy.ApplyAsync(request);

        Assert.True(request.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.Equal(["secret"], values);
    }

    [Fact]
    public async Task ApiKeyAuth_SetsCustomHeader_AndReplacesPreviousValue()
    {
        var strategy = new ApiKeyAuth("new-key", "Api-Key");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test");
        request.Headers.TryAddWithoutValidation("Api-Key", "old-key");

        await strategy.ApplyAsync(request);

        Assert.True(request.Headers.TryGetValues("Api-Key", out var values));
        Assert.Equal(["new-key"], values);
    }

    [Fact]
    public async Task BearerAuth_SetsAuthorizationHeader()
    {
        var strategy = new BearerAuth("token-123");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test");

        await strategy.ApplyAsync(request);

        Assert.True(request.Headers.TryGetValues("Authorization", out var values));
        Assert.Equal(["Bearer token-123"], values);
    }

    [Fact]
    public async Task CustomHeadersAuth_SetsAllHeaders_AndReplacesPreviousValues()
    {
        var strategy = new CustomHeadersAuth(new Dictionary<string, string>
        {
            ["X-Tenant"] = "tenant-a",
            ["X-Feature"] = "beta"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test");
        request.Headers.TryAddWithoutValidation("X-Tenant", "tenant-b");

        await strategy.ApplyAsync(request);

        Assert.True(request.Headers.TryGetValues("X-Tenant", out var tenantValues));
        Assert.Equal(["tenant-a"], tenantValues);

        Assert.True(request.Headers.TryGetValues("X-Feature", out var featureValues));
        Assert.Equal(["beta"], featureValues);
    }
}
