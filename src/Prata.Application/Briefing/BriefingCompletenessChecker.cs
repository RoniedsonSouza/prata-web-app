using Prata.Application.Abstractions;
using Prata.Application.Briefing;
using Prata.Application.Sales;
using Prata.Domain.Briefing;

namespace Prata.Application.Briefing;

public sealed class BriefingCompletenessChecker(
    IOrderRepository orders,
    IBriefingTemplateRepository templates,
    IAnswerRepository answers
) : IBriefingCompletenessChecker
{
    public async Task<IReadOnlyList<string>> GetRequiredPendingAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken
    )
    {
        var order = await orders.GetByIdAsync(tenantId, orderId, cancellationToken);
        if (order is null)
            return ["PEDIDO"];

        var template = await templates.GetPublishedForServiceAsync(tenantId, order.ServiceTypeId, cancellationToken);
        if (template is null)
            return []; // sem template ainda nao bloqueia (piloto)

        var existing = await answers.ListByOrderAsync(tenantId, orderId, cancellationToken);
        var byCode = existing.ToDictionary(a => a.QuestionCode, StringComparer.Ordinal);
        var pending = new List<string>();

        foreach (var q in template.Questions.Where(q => q.IsRequired).OrderBy(q => q.SortOrder))
        {
            if (!VisibleWhenEvaluator.IsVisible(q, byCode))
                continue;
            if (!byCode.ContainsKey(q.Code))
                pending.Add(q.Code);
        }

        return pending;
    }
}
