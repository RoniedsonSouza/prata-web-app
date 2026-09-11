using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Prata.Application.Abstractions;
using Prata.Infrastructure.Notifications;
using Prata.Infrastructure.Persistence;

namespace Prata.Application.Tests.Briefing;

public class SensitiveDataLoggingTests
{
    [Fact]
    public async Task RN_BRF_031_notifier_nao_loga_conteudo_sensivel_do_corpo()
    {
        const string marker = "MARKER_SENSIVEL_UNICO_XYZ_991";
        var logger = Substitute.For<ILogger<SmtpNotifier>>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Host"] = "CHANGE_ME" })
            .Build();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        // Db unused em SendEmailAsync (so em SendTransactionalEmailAsync).
        await using var db = new PrataDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<PrataDbContext>()
                .UseNpgsql("Host=127.0.0.1;Database=prata_unused;Username=x;Password=x")
                .Options,
            new MutableTenantContext()
        );

        var notifier = new SmtpNotifier(config, logger, db, clock);
        await notifier.SendEmailAsync("a@b.com", "orcamento", $"corpo com {marker}", CancellationToken.None);

        var logged = logger
            .ReceivedCalls()
            .SelectMany(c => c.GetArguments())
            .Select(a => a?.ToString() ?? string.Empty)
            .ToList();

        logged.Should().NotContain(s => s.Contains(marker, StringComparison.Ordinal));
    }
}
