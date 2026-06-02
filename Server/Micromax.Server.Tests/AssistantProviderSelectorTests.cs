using MicroMax.Server.Models;
using MicroMax.Server.Services.Assistant.Core;
using MicroMax.Server.Services.Assistant.Execution;
using MicroMax.Server.Services.Assistant.Providers;
using MicroMax.Server.Services.Assistant.Recovery;
using MicroMax.Server.Services.Assistant.Registry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Micromax.Server.Tests;

public sealed class AssistantProviderSelectorTests
{
    private static readonly AiCommandContext EmptyContext = new([], []);

    [Fact]
    public async Task UsesOllamaWhenAvailable()
    {
        var ollama = new FakeProvider(AiProviderKind.Ollama, new AssistantCommand { Mode = "Command", CommandType = "help" });
        var openAi = new FakeProvider(AiProviderKind.OpenAi, new AssistantCommand { Mode = "Command", CommandType = "open_products" });
        var selector = CreateSelector(ollama, openAi, CreateMock());

        var result = await selector.InterpretAsync(EmptyContext, "помощь", CancellationToken.None);

        Assert.Equal("Ollama", result.Provider);
        Assert.Equal("help", result.CommandType);
    }

    [Fact]
    public async Task FallsBackToOpenAiWhenOllamaFails()
    {
        var ollama = new FakeProvider(AiProviderKind.Ollama, failure: new InvalidOperationException("ollama down"));
        var openAi = new FakeProvider(AiProviderKind.OpenAi, new AssistantCommand { Mode = "Command", CommandType = "help" });
        var selector = CreateSelector(ollama, openAi, CreateMock());

        var result = await selector.InterpretAsync(EmptyContext, "помощь", CancellationToken.None);

        Assert.Equal("OpenAi", result.Provider);
        Assert.Equal("help", result.CommandType);
    }

    [Fact]
    public async Task FallsBackToMockWhenRealProvidersFail()
    {
        var ollama = new FakeProvider(AiProviderKind.Ollama, failure: new InvalidOperationException("ollama down"));
        var openAi = new FakeProvider(AiProviderKind.OpenAi, failure: new InvalidOperationException("openai down"));
        var selector = CreateSelector(ollama, openAi, CreateMock());

        var result = await selector.InterpretAsync(EmptyContext, "покажи доступные команды", CancellationToken.None);

        Assert.Equal("Mock", result.Provider);
        Assert.Equal("help", result.CommandType);
    }

    [Fact]
    public async Task ReturnsToOllamaAfterAvailabilityRestored()
    {
        var availability = new AiProviderAvailability();
        availability.MarkUnavailable(AiProviderKind.Ollama);

        var ollama = new FakeProvider(AiProviderKind.Ollama, new AssistantCommand { Mode = "Command", CommandType = "help" });
        var openAi = new FakeProvider(AiProviderKind.OpenAi, failure: new InvalidOperationException("openai down"));
        var selector = CreateSelector(availability, ollama, openAi, CreateMock());

        var first = await selector.InterpretAsync(EmptyContext, "покажи доступные команды", CancellationToken.None);
        availability.MarkAvailable(AiProviderKind.Ollama);
        var second = await selector.InterpretAsync(EmptyContext, "помощь", CancellationToken.None);

        Assert.Equal("Mock", first.Provider);
        Assert.Equal("Ollama", second.Provider);
    }

    [Fact]
    public async Task InvalidRealProviderResponseFallsBack()
    {
        var ollama = new FakeProvider(AiProviderKind.Ollama, new AssistantCommand { Mode = "Command", CommandType = "find_product", ProductId = 404 });
        var openAi = new FakeProvider(AiProviderKind.OpenAi, new AssistantCommand { Mode = "Command", CommandType = "help" });
        var selector = CreateSelector(ollama, openAi, CreateMock());

        var result = await selector.InterpretAsync(EmptyContext, "найди товар", CancellationToken.None);

        Assert.Equal("Server", result.Provider);
        Assert.Equal("Clarification", result.Mode);
    }

    [Fact]
    public async Task OllamaInferenceTimeoutWithHealthyProbeDoesNotMarkProviderUnavailable()
    {
        var availability = new AiProviderAvailability();
        var ollama = new ResilientFakeProvider();
        var openAi = new FakeProvider(AiProviderKind.OpenAi, new AssistantCommand { Mode = "Command", CommandType = "help" });
        var selector = CreateSelector(availability, ollama, openAi, CreateMock());

        var first = await selector.InterpretAsync(EmptyContext, "помощь", CancellationToken.None);
        var second = await selector.InterpretAsync(EmptyContext, "помощь", CancellationToken.None);

        Assert.Equal("OpenAi", first.Provider);
        Assert.Equal("Ollama", second.Provider);
        Assert.True(availability.IsAvailable(AiProviderKind.Ollama));
    }

    [Fact]
    public async Task RequestCancellationDoesNotFallbackToAnotherProvider()
    {
        var ollama = new CancellingFakeProvider();
        var openAi = new FakeProvider(AiProviderKind.OpenAi, new AssistantCommand { Mode = "Command", CommandType = "help" });
        var selector = CreateSelector(ollama, openAi, CreateMock());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            selector.InterpretAsync(EmptyContext, "помощь", cancellation.Token));
    }

    private static AiProviderSelector CreateSelector(params IAiCommandProvider[] providers)
    {
        return CreateSelector(new AiProviderAvailability(), providers);
    }

    private static AiProviderSelector CreateSelector(AiProviderAvailability availability, params IAiCommandProvider[] providers)
    {
        return new AiProviderSelector(
            providers,
            availability,
            CreateNormalizer(),
            NullLogger<AiProviderSelector>.Instance);
    }

    private static MockAiCommandProvider CreateMock()
    {
        return new MockAiCommandProvider(CreateRules());
    }

    private static AiCommandNormalizer CreateNormalizer()
    {
        var registry = new AiCommandRegistry();
        return new AiCommandNormalizer(registry, new AiCommandRules(registry));
    }

    private static AiCommandRules CreateRules()
    {
        return new AiCommandRules(new AiCommandRegistry());
    }

    private sealed class FakeProvider(
        AiProviderKind kind,
        AssistantCommand? command = null,
        Exception? failure = null) : IAiCommandProvider
    {
        public AiProviderKind Kind { get; } = kind;
        public bool IsRealProvider => Kind != AiProviderKind.Mock;
        public Task<bool> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(failure is null);

        public Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken)
        {
            if (failure is not null)
            {
                throw failure;
            }

            return Task.FromResult(command ?? new AssistantCommand { Mode = "Command", CommandType = "help" });
        }
    }

    private sealed class ResilientFakeProvider : IAiCommandProvider, IAiProviderFailurePolicy
    {
        private bool _failedOnce;

        public AiProviderKind Kind => AiProviderKind.Ollama;
        public bool IsRealProvider => true;

        public Task<bool> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken)
        {
            if (!_failedOnce)
            {
                _failedOnce = true;
                throw new AiProviderException(
                    AiProviderFailureReasons.InferenceTimeout,
                    TimeSpan.FromSeconds(90),
                    "timeout");
            }

            return Task.FromResult(new AssistantCommand { Mode = "Command", CommandType = "help" });
        }

        public async Task<bool> ShouldMarkUnavailableAsync(AiProviderException exception, CancellationToken cancellationToken)
        {
            return !await ProbeAsync(cancellationToken);
        }
    }

    private sealed class CancellingFakeProvider : IAiCommandProvider
    {
        public AiProviderKind Kind => AiProviderKind.Ollama;
        public bool IsRealProvider => true;

        public Task<bool> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<AssistantCommand> InterpretAsync(AiCommandContext context, string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AssistantCommand { Mode = "Command", CommandType = "help" });
        }
    }
}
