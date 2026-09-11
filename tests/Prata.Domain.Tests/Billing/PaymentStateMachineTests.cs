using AwesomeAssertions;
using Prata.Domain.Billing;
using Prata.Domain.Common;

namespace Prata.Domain.Tests.Billing;

public class InstallmentPixStateMachineTests
{
    public static TheoryData<InstallmentStatus, string> ParesPix()
    {
        var estados = new[]
        {
            InstallmentStatus.Pendente,
            InstallmentStatus.Processando,
            InstallmentStatus.Confirmado,
            InstallmentStatus.Liquidado,
            InstallmentStatus.Repassado,
            InstallmentStatus.Expirado,
            InstallmentStatus.EstornoSolicitado,
            InstallmentStatus.Estornado,
        };
        var transicoes = new[]
        {
            "MarcarProcessando",
            "Confirmar",
            "Expirar",
            "Liquidar",
            "Repassar",
            "SolicitarEstorno",
            "ConfirmarEstorno",
            "Autorizar",
            "Recusar",
            "AbrirChargeback",
        };
        var data = new TheoryData<InstallmentStatus, string>();
        foreach (var e in estados)
        foreach (var t in transicoes)
            data.Add(e, t);
        return data;
    }

    [Theory]
    [MemberData(nameof(ParesPix))]
    public void RN_FIN_maquina_pix_so_permite_transicoes_da_tabela(InstallmentStatus origem, string transicao)
    {
        var parcela = InstallmentFactory.PixEm(origem);
        var antes = parcela.Status;
        var result = parcela.Executar(transicao, PaymentMethod.Pix);

        if (PixTabela.Permite(origem, transicao))
            result.IsSuccess.Should().BeTrue($"{origem} → {transicao}");
        else
        {
            result.IsFailure.Should().BeTrue($"{origem} → {transicao} deveria falhar");
            parcela.Status.Should().Be(antes);
        }
    }
}

public class PaymentAggregateTests
{
    [Fact]
    public void RN_FIN_004_criar_pagamento_com_duas_parcelas_soma_o_total()
    {
        var payment = PaymentFactory.Deposit(Money.Brl(1000m), depositPercent: 40m);
        payment.IsSuccess.Should().BeTrue();
        payment.Value.Installments.Should().HaveCount(2);
        payment.Value.Installments.Sum(i => i.Amount.Amount).Should().Be(1000m);
        payment.Value.Installments[0].Amount.Amount.Should().Be(400m);
        payment.Value.Installments[1].Amount.Amount.Should().Be(600m);
        payment.Value.Status.Should().Be(PaymentStatus.Pendente);
    }

    [Fact]
    public void Payment_deriva_ParcialmentePago_quando_so_sinal_confirmado()
    {
        var payment = PaymentFactory.Deposit(Money.Brl(1000m), 40m).Value;
        payment.Installments[0].MarcarProcessando(PaymentMethod.Pix).IsSuccess.Should().BeTrue();
        payment.Installments[0].Confirmar(PaymentMethod.Pix, DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        payment.RecalcularStatus().IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.ParcialmentePago);
    }

    [Fact]
    public void RN_FIN_001_criar_cobranca_sem_split_falha()
    {
        var result = Payment.Create(
            tenantId: Guid.NewGuid(),
            orderId: Guid.NewGuid(),
            kind: PaymentKind.Deposit,
            total: Money.Brl(100m),
            method: PaymentMethod.Pix,
            splitSnapshot: null,
            depositPercent: 40m,
            balanceDueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            agora: DateTimeOffset.UtcNow
        );
        result.IsFailure.Should().BeTrue();
        result.Error!.Value.Code.Should().Be("SPLIT_OBRIGATORIO");
    }
}

public class PayoutKycTests
{
    [Fact]
    public void RN_FIN_030_liquidado_com_kyc_pendente_cria_BloqueadoKyc()
    {
        var payout = Payout.AgendarOuBloquear(
            tenantId: Guid.NewGuid(),
            paymentId: Guid.NewGuid(),
            amount: Money.Brl(100m),
            kyc: KycStatus.Pendente,
            agora: DateTimeOffset.UtcNow
        );
        payout.IsSuccess.Should().BeTrue();
        payout.Value.Status.Should().Be(PayoutStatus.BloqueadoKyc);
    }

    [Fact]
    public void RN_FIN_031_aprovar_kyc_libera_payout_bloqueado()
    {
        var payout = Payout.AgendarOuBloquear(Guid.NewGuid(), Guid.NewGuid(), Money.Brl(50m), KycStatus.Pendente, DateTimeOffset.UtcNow).Value;
        payout.LiberarAposKyc(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        payout.Status.Should().Be(PayoutStatus.Agendado);
    }
}

public class BookingTests
{
    [Fact]
    public void Booking_ativo_exige_intervalo_valido()
    {
        var start = DateTimeOffset.Parse("2026-10-01T10:00:00Z");
        var bad = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), start, start, travelBufferMinutes: 0);
        bad.IsFailure.Should().BeTrue();

        var ok = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), start, start.AddHours(2), 30);
        ok.IsSuccess.Should().BeTrue();
        ok.Value.Status.Should().Be(BookingStatus.Ativo);
    }
}

file static class PixTabela
{
    private static readonly HashSet<(InstallmentStatus, string)> Permitidas =
    [
        (InstallmentStatus.Pendente, "MarcarProcessando"),
        (InstallmentStatus.Processando, "Confirmar"),
        (InstallmentStatus.Processando, "Expirar"),
        (InstallmentStatus.Confirmado, "Liquidar"),
        (InstallmentStatus.Liquidado, "Repassar"),
        (InstallmentStatus.Confirmado, "SolicitarEstorno"),
        (InstallmentStatus.Liquidado, "SolicitarEstorno"),
        (InstallmentStatus.EstornoSolicitado, "ConfirmarEstorno"),
    ];

    public static bool Permite(InstallmentStatus origem, string transicao) =>
        Permitidas.Contains((origem, transicao));
}

file static class InstallmentFactory
{
    public static Installment PixEm(InstallmentStatus status)
    {
        var i = Installment
            .Create(Guid.NewGuid(), Guid.NewGuid(), 1, Money.Brl(100m), DateOnly.FromDateTime(DateTime.UtcNow), PaymentMethod.Pix)
            .Value;
        if (status == InstallmentStatus.Pendente)
            return i;

        var path = status switch
        {
            InstallmentStatus.Processando => new[] { "MarcarProcessando" },
            InstallmentStatus.Confirmado => new[] { "MarcarProcessando", "Confirmar" },
            InstallmentStatus.Expirado => new[] { "MarcarProcessando", "Expirar" },
            InstallmentStatus.Liquidado => new[] { "MarcarProcessando", "Confirmar", "Liquidar" },
            InstallmentStatus.Repassado => new[] { "MarcarProcessando", "Confirmar", "Liquidar", "Repassar" },
            InstallmentStatus.EstornoSolicitado => new[] { "MarcarProcessando", "Confirmar", "SolicitarEstorno" },
            InstallmentStatus.Estornado => new[]
            {
                "MarcarProcessando",
                "Confirmar",
                "SolicitarEstorno",
                "ConfirmarEstorno",
            },
            _ => Array.Empty<string>(),
        };

        foreach (var step in path)
        {
            var r = i.Executar(step, PaymentMethod.Pix);
            r.IsSuccess.Should().BeTrue($"falhou em {step} rumo a {status}: {r.Error}");
        }

        return i;
    }
}

file static class PaymentFactory
{
    public static Result<Payment> Deposit(Money total, decimal depositPercent)
    {
        var snap = new SplitRuleSnapshot(8m, null, platformFee: total.Percentage(8m));
        return Payment.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            PaymentKind.Deposit,
            total,
            PaymentMethod.Pix,
            snap,
            depositPercent,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            DateTimeOffset.UtcNow
        );
    }
}
