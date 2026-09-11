using Prata.Domain.Common;

namespace Prata.Domain.Tenancy;

public static class TenancyErrors
{
    public static readonly Error SlugInvalido = new(
        "TENANT_SLUG_INVALIDO",
        "Slug deve ter 3 a 40 caracteres, minusculo, letras, digitos e hifen."
    );

    public static readonly Error SlugReservado = new("TENANT_SLUG_RESERVADO", "Slug reservado pela plataforma.");

    public static readonly Error SlugImutavelAposPublicacao = new(
        "TENANT_SLUG_IMUTAVEL",
        "Slug nao pode ser alterado apos a primeira publicacao do portfolio."
    );

    public static readonly Error TenantJaPublicado = new("TENANT_JA_PUBLICADO", "Portfolio ja foi publicado.");

    public static readonly Error TenantSuspenso = new("TENANT_SUSPENSO", "Tenant esta suspenso.");

    public static readonly Error TenantJaSuspenso = new("TENANT_JA_SUSPENSO", "Tenant ja esta suspenso.");

    public static readonly Error TenantNaoSuspenso = new("TENANT_NAO_SUSPENSO", "Tenant nao esta suspenso.");

    public static readonly Error NomeObrigatorio = new("TENANT_NOME_OBRIGATORIO", "Nome do estudio e obrigatorio.");

    public static readonly Error UltimoOwner = TeamPolicy.UltimoOwner;
}
