using Prata.Domain.Common;

namespace Prata.Domain.Catalog;

public static class CatalogErrors
{
    public static readonly Error PrecoInvalido = new(
        "PACOTE_PRECO_INVALIDO",
        "Preco do pacote deve ser maior que zero com no maximo 2 casas decimais."
    );

    public static readonly Error NomeObrigatorio = new("PACOTE_NOME_OBRIGATORIO", "Nome do pacote e obrigatorio.");

    public static readonly Error FotosIncluidasInvalidas = new(
        "PACOTE_FOTOS_INVALIDAS",
        "Numero de fotos incluidas deve ser maior que zero."
    );

    public static readonly Error PublicacaoIncompleta = new(
        "PACOTE_PUBLICACAO_INCOMPLETA",
        "Pacote publicado exige nome, preco e numero de fotos incluidas."
    );

    public static readonly Error PacoteJaPublicado = new("PACOTE_JA_PUBLICADO", "Pacote ja esta publicado.");

    public static readonly Error PacoteNaoPublicado = new("PACOTE_NAO_PUBLICADO", "Pacote nao esta publicado.");

    public static readonly Error PacoteJaInativo = new("PACOTE_JA_INATIVO", "Pacote ja esta inativo.");
}
