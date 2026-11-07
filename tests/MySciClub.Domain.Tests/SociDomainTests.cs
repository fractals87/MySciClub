using MySciClub.Domain.Soci;
using Xunit;

namespace MySciClub.Domain.Tests;

public sealed class SociDomainTests
{
    [Fact]
    public void Create_ItalianBirth_GeneratesFiscalCodeAndLeavesDatabaseKeyUnset()
    {
        var socio = Socio.Crea(42, Data(), codiceCatastaleComune: "H501");

        Assert.Equal(0, socio.Id);
        Assert.Equal(42, socio.NumeroSocio);
        Assert.Equal("RSSMRA85T10H501O", socio.CodiceFiscale.Valore);
        Assert.Equal(ProvenienzaCodiceFiscale.Calcolato, socio.ProvenienzaCodiceFiscale);
        Assert.False(socio.CodiceFiscalePotenzialmenteObsoleto);
    }

    [Fact]
    public void Create_ForeignBirth_RequiresValidManualFiscalCode()
    {
        var data = Data(LuogoNascita.Estero("FR", "Lione"));

        Assert.Throws<ArgumentException>(() => Socio.Crea(1, data));
        var socio = Socio.Crea(1, data, " RSSMRA85C01H501D ");

        Assert.Equal("RSSMRA85C01H501D", socio.CodiceFiscale.Valore);
        Assert.Equal(ProvenienzaCodiceFiscale.Manuale, socio.ProvenienzaCodiceFiscale);
    }

    [Fact]
    public void Update_WhenCalculationInputsChange_PreservesManualFiscalCodeAndMarksItPotentiallyStale()
    {
        var socio = Socio.Crea(1, Data(), "RSSMRA85T10H501O", "H501");
        var changed = Data(nome: "Marco");

        socio.AggiornaDati(changed);

        Assert.Equal("RSSMRA85T10H501O", socio.CodiceFiscale.Valore);
        Assert.Equal(ProvenienzaCodiceFiscale.Manuale, socio.ProvenienzaCodiceFiscale);
        Assert.True(socio.CodiceFiscalePotenzialmenteObsoleto);
    }

    [Fact]
    public void Update_WhenGeneratedCodeInputsChange_DoesNotRecalculateWithoutExplicitChoice()
    {
        var socio = Socio.Crea(1, Data(), codiceCatastaleComune: "H501");
        var original = socio.CodiceFiscale;

        socio.AggiornaDati(Data(nome: "Marco"));

        Assert.Same(original, socio.CodiceFiscale);
        Assert.Equal(ProvenienzaCodiceFiscale.Calcolato, socio.ProvenienzaCodiceFiscale);
        Assert.True(socio.CodiceFiscalePotenzialmenteObsoleto);
    }

    [Fact]
    public void Update_RecalculationRequiresExplicitChoiceAndClearsStaleIndicator()
    {
        var socio = Socio.Crea(1, Data(), codiceCatastaleComune: "H501");
        var changed = Data(nome: "Marco");
        socio.AggiornaDati(changed);

        socio.AggiornaDati(changed, SceltaCodiceFiscale.Ricalcola, codiceCatastaleComune: "H501");

        Assert.Equal("RSSMRC85T10H501Q", socio.CodiceFiscale.Valore);
        Assert.Equal(ProvenienzaCodiceFiscale.Calcolato, socio.ProvenienzaCodiceFiscale);
        Assert.False(socio.CodiceFiscalePotenzialmenteObsoleto);
    }

    [Fact]
    public void Update_ManualReplacementIsExplicitAndClearsStaleIndicator()
    {
        var socio = Socio.Crea(1, Data(), codiceCatastaleComune: "H501");
        var changed = Data(nome: "Marco");
        socio.AggiornaDati(changed);

        socio.AggiornaDati(changed, SceltaCodiceFiscale.ImpostaManuale, "RSSMRC85T10H501Q");

        Assert.Equal("RSSMRC85T10H501Q", socio.CodiceFiscale.Valore);
        Assert.Equal(ProvenienzaCodiceFiscale.Manuale, socio.ProvenienzaCodiceFiscale);
        Assert.False(socio.CodiceFiscalePotenzialmenteObsoleto);
    }

    [Fact]
    public void Update_CannotGenerateFiscalCodeForForeignBirth()
    {
        var socio = Socio.Crea(1, Data(), codiceCatastaleComune: "H501");

        Assert.Throws<InvalidOperationException>(() => socio.AggiornaDati(
            Data(LuogoNascita.Estero("FR", null)), SceltaCodiceFiscale.Ricalcola));
    }

    [Fact]
    public void Update_ItalianToForeignBirthWithCalculatedCode_ConserveIsRejectedWithoutMutation()
    {
        var socio = Socio.Crea(1, Data(), codiceCatastaleComune: "H501");
        var originalData = socio.Dati;
        var originalCf = socio.CodiceFiscale;
        var originalSource = socio.ProvenienzaCodiceFiscale;
        var originalStale = socio.CodiceFiscalePotenzialmenteObsoleto;

        Assert.Throws<InvalidOperationException>(() => socio.AggiornaDati(Data(LuogoNascita.Estero("FR", "Lione"))));

        Assert.Same(originalData, socio.Dati);
        Assert.Same(originalCf, socio.CodiceFiscale);
        Assert.Equal(originalSource, socio.ProvenienzaCodiceFiscale);
        Assert.Equal(originalStale, socio.CodiceFiscalePotenzialmenteObsoleto);
        Assert.Equal(0, socio.Id);
        Assert.Equal(1, socio.NumeroSocio);
    }

    [Fact]
    public void Update_ItalianToForeignBirthWithCalculatedCode_ValidManualCodeSetsManualSource()
    {
        var socio = Socio.Crea(1, Data(), codiceCatastaleComune: "H501");
        var foreignData = Data(LuogoNascita.Estero("FR", "Lione"));

        socio.AggiornaDati(foreignData, SceltaCodiceFiscale.ImpostaManuale, "RSSMRA85C01H501D");

        Assert.Same(foreignData, socio.Dati);
        Assert.Equal("RSSMRA85C01H501D", socio.CodiceFiscale.Valore);
        Assert.Equal(ProvenienzaCodiceFiscale.Manuale, socio.ProvenienzaCodiceFiscale);
        Assert.False(socio.CodiceFiscalePotenzialmenteObsoleto);
    }

    [Fact]
    public void Update_ItalianToForeignBirthWithInvalidManualCode_IsRejectedWithoutMutation()
    {
        var socio = Socio.Crea(1, Data(), codiceCatastaleComune: "H501");
        var originalData = socio.Dati;
        var originalCf = socio.CodiceFiscale;

        Assert.Throws<ArgumentException>(() => socio.AggiornaDati(
            Data(LuogoNascita.Estero("FR", "Lione")), SceltaCodiceFiscale.ImpostaManuale, "RSSMRA85C01H501A"));

        Assert.Same(originalData, socio.Dati);
        Assert.Same(originalCf, socio.CodiceFiscale);
        Assert.Equal(ProvenienzaCodiceFiscale.Calcolato, socio.ProvenienzaCodiceFiscale);
        Assert.False(socio.CodiceFiscalePotenzialmenteObsoleto);
    }

    [Fact]
    public void Update_ChangingOnlyForeignBirthCity_DoesNotMarkFiscalCodeStale()
    {
        var socio = Socio.Crea(1, Data(LuogoNascita.Estero("FR", "Lione")), "RSSMRA85C01H501D");

        socio.AggiornaDati(Data(LuogoNascita.Estero("FR", "Parigi")));

        Assert.False(socio.CodiceFiscalePotenzialmenteObsoleto);
    }

    [Fact]
    public void Update_ChangingBirthCountry_MarksFiscalCodePotentiallyStale()
    {
        var socio = Socio.Crea(1, Data(LuogoNascita.Estero("FR", "Lione")), "RSSMRA85C01H501D");

        socio.AggiornaDati(Data(LuogoNascita.Estero("ES", "Madrid")));

        Assert.True(socio.CodiceFiscalePotenzialmenteObsoleto);
    }

    [Fact]
    public void Update_ChangesToSurnameBirthDateAndSex_MarkFiscalCodePotentiallyStale()
    {
        var surnameSocio = Socio.Crea(1, Data(), codiceCatastaleComune: "H501");
        surnameSocio.AggiornaDati(Data(cognome: "Bianchi"));
        Assert.True(surnameSocio.CodiceFiscalePotenzialmenteObsoleto);

        var birthDateSocio = Socio.Crea(1, Data(), codiceCatastaleComune: "H501");
        birthDateSocio.AggiornaDati(Data(dataNascita: new DateOnly(1986, 12, 10)));
        Assert.True(birthDateSocio.CodiceFiscalePotenzialmenteObsoleto);

        var sexSocio = Socio.Crea(1, Data(), codiceCatastaleComune: "H501");
        sexSocio.AggiornaDati(Data(sesso: Sesso.F));
        Assert.True(sexSocio.CodiceFiscalePotenzialmenteObsoleto);
    }

    [Fact]
    public void BirthAndResidence_EnforceCountrySpecificRequirementsAndShareComuneProvince()
    {
        Assert.Throws<ArgumentException>(() => LuogoNascita.Estero("IT", "Roma"));
        Assert.Throws<ArgumentException>(() => LuogoNascita.InComuneItaliano("FR", 1));
        Assert.Throws<ArgumentException>(() => new ComuneItaliano(2, new Paese(2, "FR", "Francia"), "Parigi", "75101", "75"));
        Assert.Throws<ArgumentException>(() => Residenza.Estera("Via Roma", "IT", null, null));

        Assert.Null(LuogoNascita.Estero("FR", null).ComuneEstero);
        Assert.Null(Residenza.Estera("1 rue", "FR", null, null).Cap);
        Assert.Equal("RM", Residenza.Italiana("Via Roma", "00100", ComuneRoma()).ProvinciaOSuddivisione);
    }

    [Fact]
    public void Citizenship_RequiresExactlyOnePrincipalAndNoDuplicates()
    {
        Assert.Throws<ArgumentException>(() => Data(cittadinanze: []));
        Assert.Throws<ArgumentException>(() => Data(cittadinanze: [new(1, false)]));
        Assert.Throws<ArgumentException>(() => Data(cittadinanze: [new(1, true), new(2, true)]));
        Assert.Throws<ArgumentException>(() => Data(cittadinanze: [new(1, true), new(1, false)]));

        Assert.Equal(2, Data(cittadinanze: [new(1, true), new(2, false)]).Cittadinanze.Count);
    }

    [Fact]
    public void PersonalData_NormalizesEmailAndPhoneWithoutPhoneFormatValidation()
    {
        var data = Data(email: "  MEMBER@EXAMPLE.COM  ", cellulare: "  x 123  ");

        Assert.Equal("member@example.com", data.Email);
        Assert.Equal("x 123", data.Cellulare);
    }

    [Fact]
    public void PersonalData_EnforcesRequiredValuesAndApprovedMaximumLengths()
    {
        Assert.Throws<ArgumentException>(() => Data(nome: new string('N', 101)));
        Assert.Throws<ArgumentException>(() => Data(email: "not an email"));
        Assert.Throws<ArgumentException>(() => Data(cellulare: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => Socio.Crea(0, Data(), codiceCatastaleComune: "H501"));
    }

    [Fact]
    public void DatabaseGeneratedPrimaryKey_IsInitiallyUnsetAndHasNoPublicSetter()
    {
        var socio = Socio.Crea(15, Data(), codiceCatastaleComune: "H501");
        var property = typeof(Socio).GetProperty(nameof(Socio.Id));

        Assert.Equal(0, socio.Id);
        Assert.NotNull(property);
        Assert.NotNull(property!.GetSetMethod(nonPublic: true));
        Assert.Null(property.GetSetMethod());
    }

    [Fact]
    public void MemberNumberCannotBeChangedByDomainUpdate()
    {
        var socio = Socio.Crea(15, Data(), codiceCatastaleComune: "H501");
        socio.AggiornaDati(Data(nome: "Marco"));

        Assert.Equal(15, socio.NumeroSocio);
    }

    private static DatiAnagraficiSocio Data(
        LuogoNascita? luogo = null,
        string nome = "Mario",
        string cognome = "Rossi",
        DateOnly? dataNascita = null,
        Sesso sesso = Sesso.M,
        string email = "mario@example.com",
        string cellulare = "+390212345678",
        IEnumerable<Cittadinanza>? cittadinanze = null) =>
        new(nome, cognome, dataNascita ?? new DateOnly(1985, 12, 10), luogo ?? LuogoNascita.InComuneItaliano("IT", 1),
            Residenza.Italiana("Via Roma 1", "00100", ComuneRoma()),
            cittadinanze ?? [new Cittadinanza(1, true)], sesso, email, cellulare);

    private static ComuneItaliano ComuneRoma() =>
        new(1, new Paese(1, "IT", "Italia"), "Roma", "H501", "RM");
}
