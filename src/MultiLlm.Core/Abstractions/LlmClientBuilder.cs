using System.Collections.ObjectModel;
using MultiLlm.Core.Events;
using MultiLlm.Core.Ops;

namespace MultiLlm.Core.Abstractions;

public sealed class LlmClientBuilder
{
    private readonly List<IModelProvider> _providers = [];
    private readonly ReadOnlyCollection<IModelProvider> _providersView;
    private readonly List<ILlmEventHook> _hooks = [];
    private LlmClientResilienceOptions? _resilienceOptions;
    private ISecretRedactor? _secretRedactor;

    public LlmClientBuilder()
    {
        _providersView = _providers.AsReadOnly();
    }

    public IReadOnlyList<IModelProvider> Providers => _providersView;

    public static LlmClientBuilder Create() => new();

    public LlmClientBuilder Configure(IModelProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _providers.Add(provider);
        return this;
    }

    public LlmClientBuilder Configure(IEnumerable<IModelProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        foreach (var provider in providers)
        {
            Configure(provider);
        }

        return this;
    }

    public LlmClientBuilder Configure(ILlmEventHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        _hooks.Add(hook);
        return this;
    }

    public LlmClientBuilder Configure(IEnumerable<ILlmEventHook> hooks)
    {
        ArgumentNullException.ThrowIfNull(hooks);

        foreach (var hook in hooks)
        {
            Configure(hook);
        }

        return this;
    }

    public LlmClientBuilder Configure(LlmClientResilienceOptions resilienceOptions)
    {
        _resilienceOptions = resilienceOptions ?? throw new ArgumentNullException(nameof(resilienceOptions));
        return this;
    }

    public LlmClientBuilder Configure(ISecretRedactor secretRedactor)
    {
        _secretRedactor = secretRedactor ?? throw new ArgumentNullException(nameof(secretRedactor));
        return this;
    }

    public ILlmClient Build()
    {
        if (_providers.Count == 0)
        {
            throw new InvalidOperationException("At least one provider must be configured before Build().");
        }

        return new LlmClient(_providers, _hooks, _resilienceOptions, _secretRedactor);
    }
}
