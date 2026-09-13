using Prata.Domain.Common;

namespace Prata.Domain.Sales;

/// <summary>
/// Agregado central do comercial. Transicoes em docs/05 §2.
/// E2 cobre Rascunho → Aprovado (+ Recusado, Expirado, EmEspera, CanceladoPeloCliente).
/// Confirmado exige sinal + contrato (RN-COM-020) e so e alcancavel na E3.
/// </summary>
public sealed class Order : AggregateRoot, ITenantOwned
{
    private readonly List<OrderItem> _items = [];
    private readonly List<Quote> _quotes = [];
    private readonly List<Guid> _sensitiveStaffUserIds = [];

    private Order()
    {
        Total = Money.Zero();
        Subtotal = Money.Zero();
        DiscountAmount = Money.Zero();
    }

    private Order(
        Guid id,
        Guid tenantId,
        Guid clientId,
        Guid serviceTypeId,
        DateOnly intendedDate,
        DateTimeOffset createdAt
    )
        : base(id)
    {
        TenantId = tenantId;
        ClientId = clientId;
        ServiceTypeId = serviceTypeId;
        IntendedDate = intendedDate;
        CreatedAt = createdAt;
        Status = OrderStatus.Rascunho;
        Total = Money.Zero();
        Subtotal = Money.Zero();
        DiscountAmount = Money.Zero();
    }

    public Guid TenantId { get; }

    public Guid ClientId { get; }

    public Guid ServiceTypeId { get; }

    public DateOnly IntendedDate { get; private set; }

    /// <summary>Janela logistica fechada em Agendar (E5). Null ate Agendado.</summary>
    public DateTimeOffset? ScheduledStartsAt { get; private set; }

    public DateTimeOffset? ScheduledEndsAt { get; private set; }

    public int? TravelBufferMinutes { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public OrderStatus Status { get; private set; }

    public OrderStatus? StatusAntesDaEspera { get; private set; }

    public string? HoldReason { get; private set; }

    public string? RefusalReason { get; private set; }

    public string? CancellationReason { get; private set; }

    public Discount? Discount { get; private set; }

    public Money Subtotal { get; private set; }

    public Money DiscountAmount { get; private set; }

    public Money Total { get; private set; }

    /// <summary>Staff designado para ver briefing sensivel (RN-BRF-030).</summary>
    public IReadOnlyList<Guid> SensitiveStaffUserIds => _sensitiveStaffUserIds;

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<Quote> Quotes => _quotes;

    public Quote? CurrentQuote => _quotes.Count == 0 ? null : _quotes[^1];

    public static Result<Order> Create(
        Guid tenantId,
        Guid clientId,
        Guid serviceTypeId,
        DateOnly intendedDate,
        DateTimeOffset agora
    )
    {
        if (tenantId == Guid.Empty || clientId == Guid.Empty || serviceTypeId == Guid.Empty)
            return SalesErrors.TenantInvalido;

        var hoje = DateOnly.FromDateTime(agora.UtcDateTime.Date);
        if (intendedDate < hoje)
            return SalesErrors.PedidoDataPassado;

        var order = new Order(Guid.NewGuid(), tenantId, clientId, serviceTypeId, intendedDate, agora);
        order.Raise(new PedidoCriado(tenantId, order.Id, clientId));
        return order;
    }

    public Result<Unit> AdicionarItem(
        OrderItemKind kind,
        Guid catalogItemId,
        string nameSnapshot,
        Money unitPriceSnapshot,
        int quantity
    )
    {
        if (!PodeRecalcularTotal())
            return SalesErrors.PedidoTotalBloqueado;

        var item = OrderItem.Create(TenantId, Id, kind, catalogItemId, nameSnapshot, unitPriceSnapshot, quantity);
        if (item.IsFailure)
            return Result.Failure<Unit>(item.Error!.Value);

        _items.Add(item.Value);
        return RecalcularTotal();
    }

    public Result<Unit> RemoverItem(Guid itemId)
    {
        if (!PodeRecalcularTotal())
            return SalesErrors.PedidoTotalBloqueado;

        var idx = _items.FindIndex(i => i.Id == itemId);
        if (idx < 0)
            return Error.Validation("ITEM_NAO_ENCONTRADO", "Item do pedido nao encontrado.");

        _items.RemoveAt(idx);
        return RecalcularTotal();
    }

    public Result<Unit> AplicarDesconto(Discount discount)
    {
        if (!PodeRecalcularTotal())
            return SalesErrors.PedidoTotalBloqueado;

        var previous = Discount;
        Discount = discount;
        var recalc = RecalcularTotal();
        if (recalc.IsFailure)
        {
            Discount = previous;
            RecalcularTotal();
            return recalc;
        }

        return Unit.Value;
    }

    public Result<Unit> RemoverDesconto()
    {
        if (!PodeRecalcularTotal())
            return SalesErrors.PedidoTotalBloqueado;

        Discount = null;
        return RecalcularTotal();
    }

    /// <summary>Rascunho → Enviado. Exige briefing sem pendencias obrigatorias (RN-COM-011 / RN-BRF-010).</summary>
    public Result<Unit> Enviar(IReadOnlyCollection<string> pendenciasObrigatorias)
    {
        if (Status != OrderStatus.Rascunho)
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.Enviado));

        if (pendenciasObrigatorias.Count > 0)
            return SalesErrors.BriefingIncompleto;

        Status = OrderStatus.Enviado;
        Raise(new PedidoEnviado(TenantId, Id));
        return Unit.Value;
    }

    public Result<Unit> Analisar()
    {
        if (Status != OrderStatus.Enviado)
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.EmAnalise));

        Status = OrderStatus.EmAnalise;
        Raise(new PedidoEmAnalise(TenantId, Id));
        return Unit.Value;
    }

    public Result<Unit> Recusar(string motivo)
    {
        if (Status is not (OrderStatus.Enviado or OrderStatus.EmAnalise))
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.Recusado));

        if (string.IsNullOrWhiteSpace(motivo))
            return SalesErrors.MotivoObrigatorio;

        RefusalReason = motivo.Trim();
        Status = OrderStatus.Recusado;
        Raise(new PedidoRecusado(TenantId, Id, RefusalReason));
        return Unit.Value;
    }

    public Result<Unit> EnviarOrcamento(DateTimeOffset agora, int validadeDias)
    {
        if (Status != OrderStatus.EmAnalise)
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.OrcamentoEnviado));

        if (_items.Count == 0)
            return SalesErrors.OrcamentoSemItens;

        var recalc = RecalcularTotal();
        if (recalc.IsFailure)
            return recalc;

        if (Total.Amount <= 0)
            return SalesErrors.OrcamentoTotalInvalido;

        var quote = Quote.Create(TenantId, Id, version: 1, agora, validadeDias, Total);
        if (quote.IsFailure)
            return Result.Failure<Unit>(quote.Error!.Value);

        _quotes.Add(quote.Value);
        Status = OrderStatus.OrcamentoEnviado;
        Raise(new OrcamentoEnviadoEvent(TenantId, Id, quote.Value.Version, quote.Value.ValidoAte));
        return Unit.Value;
    }

    public Result<Unit> RevisarOrcamento(DateTimeOffset agora, int validadeDias)
    {
        if (Status != OrderStatus.OrcamentoEnviado)
            return SalesErrors.TransicaoInvalida(Status, "OrcamentoRevisado");

        if (_items.Count == 0)
            return SalesErrors.OrcamentoSemItens;

        var recalc = RecalcularTotal();
        if (recalc.IsFailure)
            return recalc;

        if (Total.Amount <= 0)
            return SalesErrors.OrcamentoTotalInvalido;

        var nextVersion = (CurrentQuote?.Version ?? 0) + 1;
        var quote = Quote.Create(TenantId, Id, nextVersion, agora, validadeDias, Total);
        if (quote.IsFailure)
            return Result.Failure<Unit>(quote.Error!.Value);

        _quotes.Add(quote.Value);
        Raise(new OrcamentoRevisado(TenantId, Id, quote.Value.Version, quote.Value.ValidoAte));
        return Unit.Value;
    }

    public Result<Unit> Aprovar(DateTimeOffset agora)
    {
        if (Status != OrderStatus.OrcamentoEnviado)
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.Aprovado));

        if (CurrentQuote is null || !CurrentQuote.EstaVigente(agora))
            return SalesErrors.OrcamentoExpirado;

        Status = OrderStatus.Aprovado;
        Raise(new OrcamentoAprovado(TenantId, Id));
        return Unit.Value;
    }

    public Result<Unit> Expirar(DateTimeOffset agora)
    {
        if (Status != OrderStatus.OrcamentoEnviado)
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.Expirado));

        if (CurrentQuote is null)
            return SalesErrors.OrcamentoExpirado;

        if (CurrentQuote.EstaVigente(agora))
            return SalesErrors.OrcamentoAindaVigente;

        const string motivo = "Expirado por validade de orcamento";
        Status = OrderStatus.Expirado;
        Raise(new PedidoExpirado(TenantId, Id, motivo));
        return Unit.Value;
    }

    public Result<Unit> ColocarEmEspera(string motivo)
    {
        if (Status is not (OrderStatus.OrcamentoEnviado or OrderStatus.Aprovado))
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.EmEspera));

        if (string.IsNullOrWhiteSpace(motivo))
            return SalesErrors.MotivoObrigatorio;

        StatusAntesDaEspera = Status;
        HoldReason = motivo.Trim();
        Status = OrderStatus.EmEspera;
        Raise(new PedidoEmEspera(TenantId, Id, HoldReason, StatusAntesDaEspera.Value));
        return Unit.Value;
    }

    public Result<Unit> Retomar()
    {
        if (Status != OrderStatus.EmEspera || StatusAntesDaEspera is null)
            return SalesErrors.TransicaoInvalida(Status, "Retomado");

        var destino = StatusAntesDaEspera.Value;
        Status = destino;
        StatusAntesDaEspera = null;
        HoldReason = null;
        Raise(new PedidoRetomado(TenantId, Id, destino));
        return Unit.Value;
    }

    public Result<Unit> CancelarPeloCliente(string motivo)
    {
        if (Status is not (OrderStatus.Aprovado or OrderStatus.Confirmado))
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.CanceladoPeloCliente));

        if (string.IsNullOrWhiteSpace(motivo))
            return SalesErrors.MotivoObrigatorio;

        CancellationReason = motivo.Trim();
        Status = OrderStatus.CanceladoPeloCliente;
        Raise(new PedidoCancelado(TenantId, Id, CancellationReason, PeloCliente: true));
        return Unit.Value;
    }

    /// <summary>
    /// Aprovado → Confirmado. So com sinal Confirmado E contrato Assinado (RN-COM-020).
    /// Flags vem da aplicacao (Order nao referencia Payment/Contract).
    /// </summary>
    public Result<Unit> Confirmar(bool sinalConfirmado, bool contratoAssinado, DateTimeOffset agora)
    {
        _ = agora;
        if (Status != OrderStatus.Aprovado)
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.Confirmado));

        if (!sinalConfirmado || !contratoAssinado)
            return SalesErrors.ConfirmacaoIncompleta;

        Status = OrderStatus.Confirmado;
        Raise(new PedidoConfirmado(TenantId, Id));
        return Unit.Value;
    }

    /// <summary>
    /// Confirmado → Agendado: detalhe logistico (horario, buffer, equipe) — E5.
    /// </summary>
    public Result<Unit> Agendar(DateTimeOffset scheduledStartsAt, DateTimeOffset scheduledEndsAt, int travelBufferMinutes)
    {
        if (Status != OrderStatus.Confirmado)
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.Agendado));
        if (scheduledEndsAt <= scheduledStartsAt)
            return Error.Validation("PEDIDO_AGENDA_INTERVALO", "Fim do agendamento deve ser apos o inicio.");
        if (travelBufferMinutes < 0)
            return Error.Validation("PEDIDO_AGENDA_BUFFER", "Buffer de deslocamento nao pode ser negativo.");

        ScheduledStartsAt = scheduledStartsAt;
        ScheduledEndsAt = scheduledEndsAt;
        TravelBufferMinutes = travelBufferMinutes;
        Status = OrderStatus.Agendado;
        Raise(new AgendamentoDetalhado(TenantId, Id, scheduledStartsAt, scheduledEndsAt, travelBufferMinutes));
        return Unit.Value;
    }

    /// <summary>Confirmado|Agendado → Reagendado; libera e reserva nova data na aplicacao.</summary>
    public Result<Unit> Reagendar(DateOnly novaDataPretendida, DateTimeOffset agora)
    {
        if (Status is not (OrderStatus.Confirmado or OrderStatus.Agendado))
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.Reagendado));
        if (novaDataPretendida < DateOnly.FromDateTime(agora.UtcDateTime.Date))
            return SalesErrors.PedidoDataPassado;

        IntendedDate = novaDataPretendida;
        ScheduledStartsAt = null;
        ScheduledEndsAt = null;
        TravelBufferMinutes = null;
        Status = OrderStatus.Reagendado;
        Raise(new PedidoReagendado(TenantId, Id, novaDataPretendida));
        return Unit.Value;
    }

    /// <summary>Reagendado → Confirmado quando a nova data e aceita (sistema).</summary>
    public Result<Unit> ReconfirmarAposReagendamento()
    {
        if (Status != OrderStatus.Reagendado)
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.Confirmado));

        Status = OrderStatus.Confirmado;
        Raise(new PedidoConfirmado(TenantId, Id));
        return Unit.Value;
    }

    /// <summary>
    /// Confirmado|Agendado → Realizado. Exige agora ≥ data pretendida do evento.
    /// </summary>
    public Result<Unit> MarcarRealizado(DateTimeOffset agora)
    {
        if (Status is not (OrderStatus.Confirmado or OrderStatus.Agendado))
            return SalesErrors.TransicaoInvalida(Status, nameof(OrderStatus.Realizado));

        var hoje = DateOnly.FromDateTime(agora.UtcDateTime.Date);
        if (hoje < IntendedDate)
            return Error.Validation(
                "PEDIDO_EVENTO_FUTURO",
                "Nao e possivel marcar Realizado antes da data pretendida."
            );

        Status = OrderStatus.Realizado;
        Raise(new PedidoRealizado(TenantId, Id));
        return Unit.Value;
    }

    /// <summary>RN-BRF-030 — owner sempre ve; staff so se designado neste pedido.</summary>
    public bool PodeVerBriefingSensivel(Guid userId, bool isOwner) =>
        isOwner || _sensitiveStaffUserIds.Contains(userId);

    public Result<Unit> DesignarStaffSensivel(Guid userId)
    {
        if (userId == Guid.Empty)
            return Error.Validation("STAFF_INVALIDO", "Usuario de staff invalido.");

        if (!_sensitiveStaffUserIds.Contains(userId))
            _sensitiveStaffUserIds.Add(userId);

        return Unit.Value;
    }

    public Result<Unit> RemoverStaffSensivel(Guid userId)
    {
        _sensitiveStaffUserIds.Remove(userId);
        return Unit.Value;
    }

    /// <summary>
    /// Conflito de data ate a E5: checa pedidos Confirmado do tenant (RN-AGD-030).
    /// A aplicacao passa a lista de datas ja reservadas.
    /// </summary>
    public static Result<Unit> VerificarConflitoDeData(
        DateOnly intendedDate,
        IReadOnlyCollection<DateOnly> datasConfirmadasDoTenant
    )
    {
        if (datasConfirmadasDoTenant.Contains(intendedDate))
            return new Error(
                "PEDIDO_DATA_CONFLITO",
                "Ja existe pedido Confirmado nesta data para o tenant."
            );

        return Unit.Value;
    }

    private bool PodeRecalcularTotal() =>
        Status
            is OrderStatus.Rascunho
                or OrderStatus.Enviado
                or OrderStatus.EmAnalise
                or OrderStatus.OrcamentoEnviado;

    private Result<Unit> RecalcularTotal()
    {
        if (!PodeRecalcularTotal())
            return SalesErrors.PedidoTotalBloqueado;

        var currency = _items.Count > 0 ? _items[0].UnitPriceSnapshot.Currency : Currency.Brl;
        var subtotal = Money.Zero(currency);
        foreach (var item in _items)
            subtotal = subtotal.Add(item.LineTotal);

        Subtotal = subtotal;

        if (Discount is null)
        {
            DiscountAmount = Money.Zero(currency);
            Total = subtotal;
            return Unit.Value;
        }

        var discountAmount = Discount.ApplyTo(subtotal);
        if (discountAmount.Amount > subtotal.Amount)
            return SalesErrors.PedidoDescontoExcede;

        DiscountAmount = discountAmount;
        Total = subtotal.Subtract(discountAmount);
        return Unit.Value;
    }
}
