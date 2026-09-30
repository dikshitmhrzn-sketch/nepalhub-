namespace NepalHub.Api.Data.Entities;

public sealed class Province
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<District> Districts { get; set; } = new List<District>();
}
