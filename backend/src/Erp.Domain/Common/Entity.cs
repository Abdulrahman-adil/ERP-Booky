namespace Erp.Domain.Common;

public abstract class Entity
{
    protected Entity(Guid id = default)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
    }

    public Guid Id { get; private set; }
}
