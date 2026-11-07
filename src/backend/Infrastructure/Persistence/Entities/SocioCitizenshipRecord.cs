namespace MySciClub.Infrastructure.Persistence.Entities;

internal sealed class SocioCitizenshipRecord
{
    public long SocioId { get; set; }
    public long CountryId { get; set; }
    public bool Principale { get; set; }
    public SocioRecord Socio { get; set; } = null!;
    public CountryRecord Country { get; set; } = null!;
}
