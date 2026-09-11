using Microsoft.EntityFrameworkCore;
using Prata.Application.Briefing;
using Prata.Domain.Briefing;
using Prata.Domain.Sales;

namespace Prata.Infrastructure.Persistence;

internal sealed class BriefingTemplateRepository(PrataDbContext db) : IBriefingTemplateRepository
{
    public Task<BriefingTemplate?> GetPublishedForServiceAsync(
        Guid tenantId,
        Guid serviceTypeId,
        CancellationToken cancellationToken = default
    ) =>
        db
            .BriefingTemplates.Include("_questions")
            .Where(t => t.TenantId == tenantId && t.ServiceTypeId == serviceTypeId && t.IsPublished)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<BriefingTemplate?> GetByIdAsync(
        Guid tenantId,
        Guid templateId,
        CancellationToken cancellationToken = default
    ) =>
        db
            .BriefingTemplates.Include("_questions")
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == templateId, cancellationToken);

    public async Task<IReadOnlyList<BriefingTemplate>> ListByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) =>
        await db
            .BriefingTemplates.AsNoTracking()
            .Include("_questions")
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.Name)
            .ThenByDescending(t => t.Version)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(BriefingTemplate template, CancellationToken cancellationToken = default)
    {
        await db.BriefingTemplates.AddAsync(template, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<BriefingTemplate> templates, CancellationToken cancellationToken = default)
    {
        await db.BriefingTemplates.AddRangeAsync(templates, cancellationToken);
    }
}

internal sealed class AnswerRepository(PrataDbContext db) : IAnswerRepository
{
    public async Task<IReadOnlyList<Answer>> ListByOrderAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken = default
    ) =>
        await db
            .BriefingAnswers.Where(a => a.TenantId == tenantId && a.OrderId == orderId)
            .ToListAsync(cancellationToken);

    public Task<Answer?> GetByQuestionAsync(
        Guid tenantId,
        Guid orderId,
        string questionCode,
        CancellationToken cancellationToken = default
    ) =>
        db.BriefingAnswers.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.OrderId == orderId && a.QuestionCode == questionCode,
            cancellationToken
        );

    public async Task UpsertAsync(Answer answer, CancellationToken cancellationToken = default)
    {
        var existing = await db.BriefingAnswers.FirstOrDefaultAsync(
            a => a.TenantId == answer.TenantId && a.OrderId == answer.OrderId && a.QuestionCode == answer.QuestionCode,
            cancellationToken
        );
        if (existing is not null)
            db.BriefingAnswers.Remove(existing);

        await db.BriefingAnswers.AddAsync(answer, cancellationToken);
    }

    public async Task RemoveSensitiveByOrderAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var sensitive = await db
            .BriefingAnswers.Where(a => a.TenantId == tenantId && a.OrderId == orderId && a.IsSensitive)
            .ToListAsync(cancellationToken);
        db.BriefingAnswers.RemoveRange(sensitive);
    }

    public async Task<int> PurgeSensitiveDeliveredBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default
    )
    {
        // Pedidos entregues/concluidos com DeliveredAt antigo — ate E4 DeliveredAt pode ser nulo.
        var orderIds = await db
            .Orders.IgnoreQueryFilters()
            .Where(o =>
                (o.Status == OrderStatus.Entregue || o.Status == OrderStatus.Concluido)
                && o.CreatedAt < cutoff
            )
            .Select(o => new { o.Id, o.TenantId })
            .ToListAsync(cancellationToken);

        var purged = 0;
        foreach (var group in orderIds.GroupBy(o => o.TenantId))
        {
            var ids = group.Select(x => x.Id).ToList();
            var answers = await db
                .BriefingAnswers.IgnoreQueryFilters()
                .Where(a => a.TenantId == group.Key && a.IsSensitive && ids.Contains(a.OrderId))
                .ToListAsync(cancellationToken);
            if (answers.Count == 0)
                continue;

            db.BriefingAnswers.RemoveRange(answers);
            db.AuditLogs.Add(
                new AuditLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = group.Key,
                    Action = "ExpurgarBriefingSensivel",
                    EntityType = "Answer",
                    EntityId = null,
                    At = DateTimeOffset.UtcNow,
                    Metadata = $"{{\"count\":{answers.Count}}}",
                }
            );
            purged += answers.Count;
        }

        if (purged > 0)
            await db.SaveChangesAsync(cancellationToken);

        return purged;
    }
}

internal sealed class BriefingConsentRepository(PrataDbContext db) : IBriefingConsentRepository
{
    public Task<BriefingConsent?> GetActiveAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken = default
    ) =>
        db.BriefingConsents.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.OrderId == orderId && c.RevokedAt == null,
            cancellationToken
        );

    public async Task AddAsync(BriefingConsent consent, CancellationToken cancellationToken = default)
    {
        await db.BriefingConsents.AddAsync(consent, cancellationToken);
    }
}
