namespace MySciClub.Domain.Soci;

public sealed record Cittadinanza
{
    public long PaeseId { get; }
    public bool Principale { get; }

    public Cittadinanza(long paeseId, bool principale)
    {
        if (paeseId <= 0) throw new ArgumentOutOfRangeException(nameof(paeseId));
        PaeseId = paeseId;
        Principale = principale;
    }
}
