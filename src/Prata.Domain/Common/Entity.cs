namespace Prata.Domain.Common;

/// <summary>
/// Entidade com identidade. Comparacao por Id.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    /// <summary>Construtor para materializacao do EF Core.</summary>
    protected Entity() { }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id de entidade nao pode ser vazio.", nameof(id));

        Id = id;
    }

    public Guid Id { get; protected set; }

    public bool Equals(Entity? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (GetType() != other.GetType())
            return false;
        return Id == other.Id;
    }

    public override bool Equals(object? obj) => obj is Entity other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity? left, Entity? right) => Equals(left, right);

    public static bool operator !=(Entity? left, Entity? right) => !Equals(left, right);
}
