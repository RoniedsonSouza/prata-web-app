namespace Prata.Domain.Common;

/// <summary>
/// Valor de sucesso sem payload. Usado em <see cref="Result{T}"/> quando a
/// operação só precisa sinalizar sucesso ou falha.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value = default;

    public bool Equals(Unit other) => true;

    public override bool Equals(object? obj) => obj is Unit;

    public override int GetHashCode() => 0;

    public static bool operator ==(Unit left, Unit right) => true;

    public static bool operator !=(Unit left, Unit right) => false;
}
