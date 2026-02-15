using MultiLlm.Core.Abstractions;
using MultiLlm.Core.Auth;

namespace MultiLlm.Providers.Codex;

public static class CodexProviderBuilderExtensions
{
    public static LlmClientBuilder Configure(
        this LlmClientBuilder builder,
        CodexProviderOptions options,
        ITokenStore? tokenStore = null,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        ICodexAuthBackend[] backends =
        [
            new OfficialDeviceCodeBackend(options),
            new ExperimentalAuthBackend()
        ];

        return builder.Configure(new CodexProvider(options, backends, tokenStore, httpClient));
    }

    public static LlmClientBuilder Configure(
        this LlmClientBuilder builder,
        CodexProviderOptions options,
        IEnumerable<ICodexAuthBackend> backends,
        ITokenStore? tokenStore = null,
        HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(backends);

        return builder.Configure(new CodexProvider(options, backends, tokenStore, httpClient));
    }
}
