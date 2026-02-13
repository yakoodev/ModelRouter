using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Auth;
using MultiLlm.Core.Contracts;
using MultiLlm.Providers.OpenAICompatible;

namespace MultiLlm.Providers.Codex;

public sealed class CodexProvider : IModelProvider
{
    private readonly CodexProviderOptions _options;
    private readonly IReadOnlyList<ICodexAuthBackend> _enabledBackends;
    private readonly ITokenStore _tokenStore;
    private readonly HttpClient? _httpClient;

    public CodexProvider(
        CodexProviderOptions options,
        IEnumerable<ICodexAuthBackend> backends,
        ITokenStore? tokenStore = null,
        HttpClient? httpClient = null)
    {
        _options = options;
        _enabledBackends = CodexAuthBackendSelector.Select(options, backends);
        _tokenStore = tokenStore ?? new InMemoryTokenStore();
        _httpClient = httpClient;
    }

    public string ProviderId => _options.ProviderId;

    public ProviderCapabilities Capabilities => new(true, true, true, true);

    public IReadOnlyList<string> EnabledAuthBackendIds => _enabledBackends.Select(static backend => backend.BackendId).ToArray();

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var token = await ResolveAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var provider = CreateInnerProvider(token, request.Model);
        return await provider.ChatAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<ChatDelta> ChatStreamAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var token = await ResolveAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var provider = CreateInnerProvider(token, request.Model);

        await foreach (var delta in provider.ChatStreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            yield return delta;
        }
    }

    private async Task<AuthToken> ResolveAccessTokenAsync(CancellationToken cancellationToken)
    {
        foreach (var backend in _enabledBackends)
        {
            await backend.AuthenticateAsync(_tokenStore, cancellationToken).ConfigureAwait(false);
        }

        var token = await _tokenStore.GetAsync(OfficialDeviceCodeBackend.DeviceTokenStoreKey, cancellationToken).ConfigureAwait(false);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Codex token is missing after auth backend execution.");
        }

        return token;
    }

    private IModelProvider CreateInnerProvider(AuthToken token, string requestModel)
    {
        if (_options.UseChatGptBackend)
        {
            return new ChatGptBackendProvider(_options, token.AccessToken, _httpClient);
        }

        EnsureTokenCompatibleWithOpenAiCompatibleEndpoint(token);
        var configuredModel = !string.IsNullOrWhiteSpace(_options.Model) ? _options.Model : requestModel;
        var providerOptions = new OpenAiCompatibleProviderOptions
        {
            ProviderId = ProviderId,
            BaseUrl = _options.BaseUrl,
            Model = configuredModel,
            Timeout = _options.Timeout,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Authorization"] = $"Bearer {token.AccessToken}"
            }
        };

        return new OpenAiCompatibleProvider(providerOptions, _httpClient);
    }

    private void EnsureTokenCompatibleWithOpenAiCompatibleEndpoint(AuthToken token)
    {
        var isDeviceCodeToken = !string.IsNullOrWhiteSpace(token.RefreshToken);
        if (!isDeviceCodeToken)
        {
            return;
        }

        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var endpoint))
        {
            return;
        }

        if (string.Equals(endpoint.Host, "api.openai.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Вы используете ChatGPT device-code токен из Codex CLI с OpenAI-compatible endpoint (api.openai.com/v1). " +
                "Для этого endpoint нужен API key. Запустите с --auth apikey (или OPENAI_API_KEY), " +
                "либо включите UseChatGptBackend в CodexProviderOptions.");
        }
    }
}
