namespace NepalHub.Api.Data.Entities;

public sealed class District
{
    public Guid Id { get; set; }

    public Guid ProvinceId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public Province Province { get; set; } = null!;
}
