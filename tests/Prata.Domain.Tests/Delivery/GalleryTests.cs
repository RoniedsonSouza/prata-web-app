using AwesomeAssertions;
using Prata.Domain.Common;
using Prata.Domain.Delivery;

namespace Prata.Domain.Tests.Delivery;

public class GalleryStateMachineTests
{
    public static TheoryData<GalleryStatus, string> Pares()
    {
        var estados = Enum.GetValues<GalleryStatus>();
        var transicoes = new[]
        {
            "Disponibilizar",
            "BloquearPorPendencia",
            "Desbloquear",
            "IniciarSelecao",
            "FecharSelecao",
            "Entregar",
            "Expirar",
            "Arquivar",
            "Reativar",
        };
        var data = new TheoryData<GalleryStatus, string>();
        foreach (var e in estados)
        foreach (var t in transicoes)
            data.Add(e, t);
        return data;
    }

    [Theory]
    [MemberData(nameof(Pares))]
    public void RN_ENT_maquina_galeria_so_permite_pares_da_tabela(GalleryStatus origem, string transicao)
    {
        var gallery = GalleryFactory.Em(origem);
        var antes = gallery.Status;
        var result = gallery.Executar(transicao, GalleryExecContext.Ok);

        if (GalleryTabela.Permite(origem, transicao))
            result.IsSuccess.Should().BeTrue($"{origem} → {transicao}");
        else
        {
            result.IsFailure.Should().BeTrue();
            gallery.Status.Should().Be(antes);
        }
    }

    [Fact]
    public void RN_ENT_010_disponibilizar_sem_derivadas_falha()
    {
        var g = GalleryFactory.Em(GalleryStatus.EmPreparo);
        g.AdicionarFoto(Photo.Create(g.TenantId, g.Id, "key", 100, "hash1", 1000, 800, 0).Value);
        var r = g.Disponibilizar(todasDerivadasProntas: false);
        r.IsFailure.Should().BeTrue();
        r.Error!.Value.Code.Should().Be("GALERIA_DERIVADAS_PENDENTES");
    }

    [Fact]
    public void RN_ENT_020_fechar_selecao_acima_do_limite_sem_upsell_falha()
    {
        var g = GalleryFactory.Em(GalleryStatus.EmSelecao, photoLimit: 2);
        g.Selecionar(Guid.NewGuid(), Guid.NewGuid()).IsSuccess.Should().BeTrue();
        g.Selecionar(Guid.NewGuid(), Guid.NewGuid()).IsSuccess.Should().BeTrue();
        g.Selecionar(Guid.NewGuid(), Guid.NewGuid()).IsSuccess.Should().BeTrue();
        var r = g.FecharSelecao(upsellConfirmado: false, peloJob: false);
        r.IsFailure.Should().BeTrue();
        r.Error!.Value.Code.Should().Be("SELECAO_EXCEDE_LIMITE");
    }

    [Fact]
    public void RN_ENT_020_excedente_com_upsell_por_foto_fecha()
    {
        var g = GalleryFactory.Em(GalleryStatus.EmSelecao, photoLimit: 1);
        g.Selecionar(Guid.NewGuid(), Guid.NewGuid()).IsSuccess.Should().BeTrue();
        g.Selecionar(Guid.NewGuid(), Guid.NewGuid()).IsSuccess.Should().BeTrue();
        g.FecharSelecao(upsellConfirmado: true, peloJob: false).IsSuccess.Should().BeTrue();
        g.Status.Should().Be(GalleryStatus.SelecaoFechada);
        g.ExtraPhotosCount.Should().Be(1);
    }

    [Fact]
    public void RN_ENT_021_original_so_com_selecao_fechada_e_saldo()
    {
        var g = GalleryFactory.Em(GalleryStatus.EmSelecao);
        g.PodeBaixarOriginal(saldoConfirmado: true).Should().BeFalse();

        g.FecharSelecao(false, false).IsSuccess.Should().BeTrue();
        g.PodeBaixarOriginal(saldoConfirmado: false).Should().BeFalse();
        g.PodeBaixarOriginal(saldoConfirmado: true).Should().BeTrue();
    }

    [Fact]
    public void RN_ENT_032_bloqueada_abre_baixa_trava_alta()
    {
        var g = GalleryFactory.Em(GalleryStatus.Disponivel);
        g.BloquearPorPendencia().IsSuccess.Should().BeTrue();
        g.Status.Should().Be(GalleryStatus.BloqueadaPorPendencia);
        g.PermiteAltaResolucao.Should().BeFalse();
        g.PermiteVisualizacaoBaixa.Should().BeTrue();
    }

    [Fact]
    public void RN_ENT_030_031_portfolio_bloqueado_por_consent_ou_menor()
    {
        var consentNo = Gallery.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            photoLimit: 10,
            PortfolioConsent.Nao,
            hasMinor: false,
            expiresAt: DateTimeOffset.UtcNow.AddMonths(12),
            DateTimeOffset.UtcNow
        ).Value;
        consentNo.PodePublicarNoPortfolio.Should().BeFalse();

        var minor = Gallery.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            10,
            PortfolioConsent.Sim,
            hasMinor: true,
            DateTimeOffset.UtcNow.AddMonths(12),
            DateTimeOffset.UtcNow
        ).Value;
        minor.PodePublicarNoPortfolio.Should().BeFalse();
    }
}

public class ShareLinkTests
{
    [Fact]
    public void RN_ENT_040_senha_curta_falha()
    {
        var r = ShareLink.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            password: "12345",
            tokenPlain: "tok",
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            allowFavorites: true,
            allowWebDownload: true,
            DateTimeOffset.UtcNow
        );
        r.IsFailure.Should().BeTrue();
        r.Error!.Value.Code.Should().Be("SHARE_SENHA_CURTA");
    }

    [Fact]
    public void RN_ENT_040_convidado_nao_baixa_original()
    {
        var link = ShareLink
            .Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "segredo",
                "token-abc",
                DateTimeOffset.UtcNow.AddDays(7),
                true,
                allowWebDownload: true,
                DateTimeOffset.UtcNow
            )
            .Value;
        link.PodeBaixarOriginal.Should().BeFalse();
        link.AllowWebDownload.Should().BeTrue();
    }
}

public class PhotoVariantTests
{
    [Fact]
    public void RN_ENT_003_original_nao_e_sobrescrito_por_variante()
    {
        var photo = Photo.Create(Guid.NewGuid(), Guid.NewGuid(), "orig/key", 10, "abc", 100, 100, 0).Value;
        var hashAntes = photo.OriginalHash;
        photo.RegistrarVariante(PhotoVariantKind.Thumb, "thumb/key", 480, 320, 1, false, DateTimeOffset.UtcNow)
            .IsSuccess.Should()
            .BeTrue();
        photo.OriginalHash.Should().Be(hashAntes);
        photo.OriginalKey.Should().Be("orig/key");
    }
}

file static class GalleryTabela
{
    private static readonly HashSet<(GalleryStatus, string)> Permitidas =
    [
        (GalleryStatus.EmPreparo, "Disponibilizar"),
        (GalleryStatus.Disponivel, "BloquearPorPendencia"),
        (GalleryStatus.BloqueadaPorPendencia, "Desbloquear"),
        (GalleryStatus.Disponivel, "IniciarSelecao"),
        (GalleryStatus.EmSelecao, "FecharSelecao"),
        (GalleryStatus.SelecaoFechada, "Entregar"),
        (GalleryStatus.Disponivel, "Expirar"),
        (GalleryStatus.EmSelecao, "Expirar"),
        (GalleryStatus.SelecaoFechada, "Expirar"),
        (GalleryStatus.Entregue, "Expirar"),
        (GalleryStatus.BloqueadaPorPendencia, "Expirar"),
        (GalleryStatus.Expirada, "Arquivar"),
        (GalleryStatus.Arquivada, "Reativar"),
    ];

    public static bool Permite(GalleryStatus o, string t) => Permitidas.Contains((o, t));
}

file static class GalleryFactory
{
    public static Gallery Em(GalleryStatus status, int photoLimit = 50)
    {
        var g = Gallery
            .Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                photoLimit,
                PortfolioConsent.Sim,
                hasMinor: false,
                DateTimeOffset.UtcNow.AddMonths(12),
                DateTimeOffset.UtcNow
            )
            .Value;

        if (status == GalleryStatus.EmPreparo)
            return g;

        // Caminho feliz minimo ate o status pedido.
        void Step(string name)
        {
            var r = g.Executar(name, GalleryExecContext.Ok);
            r.IsSuccess.Should().BeTrue($"falhou {name} rumo a {status}: {r.Error}");
        }

        switch (status)
        {
            case GalleryStatus.Disponivel:
                Step("Disponibilizar");
                break;
            case GalleryStatus.BloqueadaPorPendencia:
                Step("Disponibilizar");
                Step("BloquearPorPendencia");
                break;
            case GalleryStatus.EmSelecao:
                Step("Disponibilizar");
                Step("IniciarSelecao");
                break;
            case GalleryStatus.SelecaoFechada:
                Step("Disponibilizar");
                Step("IniciarSelecao");
                Step("FecharSelecao");
                break;
            case GalleryStatus.Entregue:
                Step("Disponibilizar");
                Step("IniciarSelecao");
                Step("FecharSelecao");
                Step("Entregar");
                break;
            case GalleryStatus.Expirada:
                Step("Disponibilizar");
                Step("Expirar");
                break;
            case GalleryStatus.Arquivada:
                Step("Disponibilizar");
                Step("Expirar");
                Step("Arquivar");
                break;
        }

        return g;
    }
}
