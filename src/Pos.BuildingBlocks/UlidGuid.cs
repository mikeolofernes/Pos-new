using NUlid;

namespace Pos.BuildingBlocks;

public static class UlidGuid
{
    public static Guid NewUlidGuid() => Ulid.NewUlid().ToGuid();
    public static Guid From(DateTimeOffset ts) => Ulid.NewUlid(ts).ToGuid();
}
