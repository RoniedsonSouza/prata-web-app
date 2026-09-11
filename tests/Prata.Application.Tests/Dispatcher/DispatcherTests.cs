using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Prata.Application.Common;
using Prata.Domain.Common;

namespace Prata.Application.Tests.Dispatcher;

public class DispatcherTests
{
    private sealed record PingCommand(string Message) : ICommand<string>;

    private sealed class PingHandler : ICommandHandler<PingCommand, string>
    {
        public Task<Result<string>> Handle(PingCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success($"pong:{command.Message}"));
    }

    [Fact]
    public async Task Dispatcher_resolve_handler_registrado()
    {
        var services = new ServiceCollection();
        services.AddPrataDispatcher();
        services.AddScoped<ICommandHandler<PingCommand, string>, PingHandler>();
        await using var provider = services.BuildServiceProvider();

        var dispatcher = provider.GetRequiredService<IDispatcher>();
        var result = await dispatcher.Send(new PingCommand("oi"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("pong:oi");
    }
}
