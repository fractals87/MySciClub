namespace MySciClub.Infrastructure.Persistence.Entities;

internal sealed class ItalianTownRecord
{
    public long Id { get; set; }
    public long CountryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CadastralCode { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public CountryRecord Country { get; set; } = null!;
}
