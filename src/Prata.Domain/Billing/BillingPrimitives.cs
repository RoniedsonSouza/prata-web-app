using Prata.Domain.Common;

namespace Prata.Domain.Billing;

public static class BillingErrors
{
    public static readonly Error SplitObrigatorio = new("SPLIT_OBRIGATORIO", "Cobranca exige SplitRule (RN-FIN-001).");
    public static readonly Error SinalForaDaFaixa = new("SINAL_FORA_DA_FAIXA", "Sinal deve estar entre 30% e 50% (RN-FIN-010).");
    public static readonly Error ParcelasNaoSomam = new("PARCELAS_NAO_SOMAM", "Soma das parcelas diverge do total (RN-FIN-004).");
    public static readonly Error TransicaoInvalida = new("PARCELA_TRANSICAO_INVALIDA", "Transicao de parcela invalida para o meio de pagamento.");
    public static readonly Error MetodoNaoSuporta = new("PARCELA_METODO_NAO_SUPORTA", "Transicao nao existe neste meio de pagamento.");
    public static readonly Error IntervaloInvalido = new("BOOKING_INTERVALO_INVALIDO", "Fim do booking deve ser depois do inicio.");
    public static readonly Error PercentInvalido = new("SPLIT_PERCENT_INVALIDO", "Percentual de comissao invalido.");
    public static readonly Error PayoutNaoBloqueado = new("PAYOUT_NAO_BLOQUEADO", "Payout nao esta BloqueadoKyc.");
}

public enum PaymentMethod
{
    Pix = 0,
    Cartao = 1,
    Boleto = 2,
}

public enum PaymentKind
{
    Deposit = 0,
    Balance = 1,
    Upsell = 2,
    Reactivation = 3,
}

public enum InstallmentStatus
{
    Pendente = 0,
    Processando = 1,
    Autorizado = 2,
    Confirmado = 3,
    Liquidado = 4,
    Repassado = 5,
    Expirado = 6,
    Recusado = 7,
    EstornoSolicitado = 8,
    Estornado = 9,
    EstornoParcial = 10,
    Chargeback = 11,
    EmDisputa = 12,
}

public enum PaymentStatus
{
    Pendente = 0,
    ParcialmentePago = 1,
    Confirmado = 2,
    Liquidado = 3,
    Cancelado = 4,
}

public enum KycStatus
{
    Pendente = 0,
    EmAnalise = 1,
    Aprovado = 2,
    Reprovado = 3,
}

public enum PayoutStatus
{
    BloqueadoKyc = 0,
    Agendado = 1,
    EmTransito = 2,
    Liquidado = 3,
    Falhou = 4,
}

public enum BookingStatus
{
    Ativo = 0,
    Cancelado = 1,
}

public static class DepositPolicy
{
    public const decimal MinPercent = 30m;
    public const decimal MaxPercent = 50m;

    public static Result<Unit> ValidatePercent(decimal percent)
    {
        if (percent < MinPercent || percent > MaxPercent)
            return BillingErrors.SinalForaDaFaixa;
        return Unit.Value;
    }
}

public static class MoneySplitter
{
    /// <summary>Divide o total em N partes iguais; resto na ultima (RN-FIN-004).</summary>
    public static IReadOnlyList<Money> SplitEvenly(Money total, int parts)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parts);

        var cents = (long)(total.Amount * 100m);
        var baseCents = cents / parts;
        var remainder = cents % parts;
        var list = new List<Money>(parts);
        for (var i = 0; i < parts; i++)
        {
            var c = baseCents + (i == parts - 1 ? remainder : 0);
            list.Add(Money.Create(c / 100m, total.Currency));
        }

        return list;
    }
}
