using MultiLlm.Providers.Codex;

namespace MultiLlm.Core.Tests;

public class CodexProviderAuthSlotTests
{
    [Fact]
    public void Constructor_Throws_WhenCodexIsEnabledInProduction()
    {
        var options = new CodexProviderOptions(IsDevelopment: false);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new CodexProvider(options, [new OfficialDeviceCodeBackend(), new ExperimentalAuthBackend()]));

        Assert.Contains("dev-only", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_UsesOnlyOfficialBackend_WhenExperimentalFlagIsDisabled()
    {
        var provider = new CodexProvider(
            new CodexProviderOptions(IsDevelopment: true, EnableExperimentalAuthAdapters: false),
            [new OfficialDeviceCodeBackend(), new ExperimentalAuthBackend()]);

        Assert.Equal([OfficialDeviceCodeBackend.BackendIdValue], provider.EnabledAuthBackendIds);
    }

    [Fact]
    public void Constructor_EnablesExperimentalBackend_WhenFlagIsEnabled()
    {
        var provider = new CodexProvider(
            new CodexProviderOptions(IsDevelopment: true, EnableExperimentalAuthAdapters: true),
            [new OfficialDeviceCodeBackend(), new ExperimentalAuthBackend()]);

        Assert.Equal(
            [OfficialDeviceCodeBackend.BackendIdValue, ExperimentalAuthBackend.BackendIdValue],
            provider.EnabledAuthBackendIds);
    }

    [Fact]
    public void Options_Defaults_KeepExperimentalBackendDisabled()
    {
        var options = new CodexProviderOptions();

        Assert.False(options.EnableExperimentalAuthAdapters);
    }
}
