using Microsoft.EntityFrameworkCore;
using Prata.Application.Abstractions;
using Prata.Application.Briefing;
using Prata.Domain.Sales;
using Prata.Infrastructure.Persistence;

namespace Prata.Infrastructure.Jobs;

public interface IPurgeSensitiveBriefingProcessor
{
    Task<int> ExpurgarAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// RN-LGP-004 — apaga respostas sensiveis 12 meses apos entrega, com auditoria.
/// </summary>
public sealed class PurgeSensitiveBriefingProcessor(
    IAnswerRepository answers,
    IDateTimeProvider clock
) : IPurgeSensitiveBriefingProcessor
{
    public Task<int> ExpurgarAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.UtcNow.AddMonths(-12);
        return answers.PurgeSensitiveDeliveredBeforeAsync(cutoff, cancellationToken);
    }
}
