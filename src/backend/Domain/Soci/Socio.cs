namespace MySciClub.Domain.Soci;

public sealed class Socio
{
    // Private setter lets EF Core populate the database-generated key without
    // exposing a Domain operation that can change the technical identity.
    public long Id { get; private set; }
    public long NumeroSocio { get; }
    public DatiAnagraficiSocio Dati { get; private set; }
    public CodiceFiscale CodiceFiscale { get; private set; }
    public ProvenienzaCodiceFiscale ProvenienzaCodiceFiscale { get; private set; }
    public bool CodiceFiscalePotenzialmenteObsoleto { get; private set; }
    public string? ChiaveFotografia { get; private set; }

    private Socio(long numeroSocio, DatiAnagraficiSocio dati, CodiceFiscale cf, ProvenienzaCodiceFiscale provenienza)
    {
        if (numeroSocio <= 0) throw new ArgumentOutOfRangeException(nameof(numeroSocio));
        NumeroSocio = numeroSocio;
        Dati = dati;
        CodiceFiscale = cf;
        ProvenienzaCodiceFiscale = provenienza;
    }

    public static Socio Crea(
        long numeroSocio,
        DatiAnagraficiSocio dati,
        string? codiceFiscaleManuale = null,
        string? codiceCatastaleComune = null)
    {
        ArgumentNullException.ThrowIfNull(dati);
        if (dati.LuogoNascita.InItalia)
        {
            var cf = string.IsNullOrWhiteSpace(codiceFiscaleManuale)
                ? CodiceFiscale.GenerateItaliano(dati.Nome, dati.Cognome, dati.DataNascita, dati.Sesso,
                    codiceCatastaleComune ?? throw new ArgumentException("È richiesto il codice catastale del comune di nascita.", nameof(codiceCatastaleComune)))
                : CodiceFiscale.Parse(codiceFiscaleManuale);
            return new(numeroSocio, dati, cf,
                string.IsNullOrWhiteSpace(codiceFiscaleManuale) ? ProvenienzaCodiceFiscale.Calcolato : ProvenienzaCodiceFiscale.Manuale);
        }

        if (string.IsNullOrWhiteSpace(codiceFiscaleManuale))
            throw new ArgumentException("Per una nascita all'estero il codice fiscale deve essere inserito manualmente.", nameof(codiceFiscaleManuale));
        return new(numeroSocio, dati, CodiceFiscale.Parse(codiceFiscaleManuale), ProvenienzaCodiceFiscale.Manuale);
    }

    public void AggiornaDati(
        DatiAnagraficiSocio nuoviDati,
        SceltaCodiceFiscale sceltaCodiceFiscale = SceltaCodiceFiscale.Conserva,
        string? codiceFiscaleManuale = null,
        string? codiceCatastaleComune = null)
    {
        ArgumentNullException.ThrowIfNull(nuoviDati);
        if (!Enum.IsDefined(sceltaCodiceFiscale)) throw new ArgumentOutOfRangeException(nameof(sceltaCodiceFiscale));

        var datiCalcoloCambiati = Dati.Nome != nuoviDati.Nome || Dati.Cognome != nuoviDati.Cognome ||
            Dati.DataNascita != nuoviDati.DataNascita || Dati.Sesso != nuoviDati.Sesso ||
            Dati.LuogoNascita.CodiceIsoPaese != nuoviDati.LuogoNascita.CodiceIsoPaese ||
            Dati.LuogoNascita.ComuneItalianoId != nuoviDati.LuogoNascita.ComuneItalianoId;

        switch (sceltaCodiceFiscale)
        {
            case SceltaCodiceFiscale.Conserva:
                if (!string.IsNullOrWhiteSpace(codiceFiscaleManuale) || !string.IsNullOrWhiteSpace(codiceCatastaleComune))
                    throw new ArgumentException("Non fornire dati CF quando la scelta è Conserva.");
                if (ProvenienzaCodiceFiscale == ProvenienzaCodiceFiscale.Calcolato &&
                    Dati.LuogoNascita.InItalia && !nuoviDati.LuogoNascita.InItalia)
                    throw new InvalidOperationException("Il CF calcolato non può essere mantenuto quando la nascita viene spostata all'estero; inserire un CF manuale valido.");
                if (datiCalcoloCambiati) CodiceFiscalePotenzialmenteObsoleto = true;
                break;
            case SceltaCodiceFiscale.Ricalcola:
                if (!nuoviDati.LuogoNascita.InItalia)
                    throw new InvalidOperationException("Il CF può essere generato automaticamente solo per nascite in Italia.");
                if (!string.IsNullOrWhiteSpace(codiceFiscaleManuale))
                    throw new ArgumentException("Non fornire un CF manuale quando è stato scelto il ricalcolo.", nameof(codiceFiscaleManuale));
                CodiceFiscale = CodiceFiscale.GenerateItaliano(
                    nuoviDati.Nome, nuoviDati.Cognome, nuoviDati.DataNascita, nuoviDati.Sesso,
                    codiceCatastaleComune ?? throw new ArgumentException("È richiesto il codice catastale del comune di nascita.", nameof(codiceCatastaleComune)));
                ProvenienzaCodiceFiscale = ProvenienzaCodiceFiscale.Calcolato;
                CodiceFiscalePotenzialmenteObsoleto = false;
                break;
            case SceltaCodiceFiscale.ImpostaManuale:
                if (string.IsNullOrWhiteSpace(codiceFiscaleManuale))
                    throw new ArgumentException("È richiesto un CF manuale.", nameof(codiceFiscaleManuale));
                if (!string.IsNullOrWhiteSpace(codiceCatastaleComune))
                    throw new ArgumentException("Non fornire il codice catastale quando il CF è inserito manualmente.", nameof(codiceCatastaleComune));
                CodiceFiscale = CodiceFiscale.Parse(codiceFiscaleManuale);
                ProvenienzaCodiceFiscale = ProvenienzaCodiceFiscale.Manuale;
                CodiceFiscalePotenzialmenteObsoleto = false;
                break;
        }

        Dati = nuoviDati;
    }

    public void ImpostaChiaveFotografia(string? storageKey)
    {
        if (storageKey is not null)
        {
            storageKey = storageKey.Trim();
            if (storageKey.Length == 0) throw new ArgumentException("La chiave storage non può essere vuota.", nameof(storageKey));
        }
        ChiaveFotografia = storageKey;
    }
}
