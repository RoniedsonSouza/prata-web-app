namespace Prata.Domain.Sales;

/// <summary>
/// Estados do pedido. Membros em portugues (ADR-0009).
/// Significado: docs/01-GLOSSARIO.md §3.
/// </summary>
public enum OrderStatus
{
    Rascunho = 0,
    Enviado = 1,
    EmAnalise = 2,
    OrcamentoEnviado = 3,
    Aprovado = 4,
    Confirmado = 5,
    Agendado = 6,
    Realizado = 7,
    EmEdicao = 8,
    Entregue = 9,
    Concluido = 10,
    EmEspera = 11,
    Recusado = 12,
    Expirado = 13,
    CanceladoPeloCliente = 14,
    CanceladoPeloEstudio = 15,
    Reagendado = 16,
}
