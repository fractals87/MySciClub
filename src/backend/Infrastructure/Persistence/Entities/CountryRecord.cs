namespace MySciClub.Infrastructure.Persistence.Entities;

internal sealed class CountryRecord
{
    public long Id { get; set; }
    public string IsoAlpha2 { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ICollection<ItalianTownRecord> ItalianTowns { get; set; } = new List<ItalianTownRecord>();
}
