namespace Pos.BuildingBlocks;

public sealed record PageRequest(int Page = 1, int PageSize = 50, string? Sort = null, string? Q = null)
{
    public int Skip => (Math.Max(1, Page) - 1) * Math.Clamp(PageSize, 1, 200);
    public int Take => Math.Clamp(PageSize, 1, 200);
}

public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int PageNumber, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)Total / Math.Max(1, PageSize));
}
