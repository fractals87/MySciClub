using MySciClub.Domain.Soci;
using Xunit;

namespace MySciClub.Domain.Tests;

public sealed class CodiceFiscaleTests
{
    // Sources for this vector set:
    // - D.M. 23/12/1976 n. 13813, arts. 3-5 and 7, official text:
    //   https://def.finanze.it/DocTribFrontend/getAttoNormativoDetail.do?ACTION=getSommario&id=%7B8D93A450-E5FE-47FC-A6F0-F938769051BF%7D
    // - The first vector is also an independently published worked example
    //   (Mario Rossi, male, 01/03/1985, Roma):
    //   https://calcolo.tools/calcolatori/codice-fiscale-inverso/
    // Other expected strings below were independently calculated from the
    // cited decree's rules; they are not represented as official assignments.
    // Combination coverage: Rossi/Mario with two dates and both sexes;
    // Lovelace/Ada covers sparse consonants; Bianchi/Francesco covers >=4
    // name consonants; D'Angelo/José covers punctuation and deterministic accents.
    [Theory]
    // Published worked vector: calcolo.tools reference above.
    [InlineData("Mario", "Rossi", 1985, 3, 1, Sesso.M, "H501", "RSSMRA85C01H501D")]
    // Published independent outputs: https://codicefiscalesubito.it/ (Rossi/Mario,
    // 10/12/1985, Roma; both M and F are shown in its example/calculation guide).
    [InlineData("Mario", "Rossi", 1985, 12, 10, Sesso.M, "H501", "RSSMRA85T10H501O")]
    [InlineData("Mario", "Rossi", 1985, 12, 10, Sesso.F, "H501", "RSSMRA85T50H501S")]
    // These fixed values are independently derived from D.M. rules cited above,
    // not copied from or described as official assignments. They vary name
    // consonant patterns, date, sex and cadastral code.
    [InlineData("Ada", "Lovelace", 1815, 2, 28, Sesso.F, "F205", "LVLDAA15B68F205I")]
    [InlineData("Francesco", "Bianchi", 2000, 12, 31, Sesso.M, "F205", "BNCFNC00T31F205I")]
    [InlineData("José", "D'Ángelo", 1985, 12, 10, Sesso.M, "H501", "DNGJSO85T10H501A")]
    public void GenerateItaliano_MatchesIndependentReferenceVectors(
        string nome, string cognome, int year, int month, int day, Sesso sesso, string cadastral, string expected)
    {
        var actual = CodiceFiscale.GenerateItaliano(nome, cognome, new DateOnly(year, month, day), sesso, cadastral);

        Assert.Equal(expected, actual.Valore);
    }

    [Fact]
    public void Parse_AcceptsPublishedMultiPositionOmocodiaVector()
    {
        // calcolo.tools publishes this worked omocodia example alongside its
        // decoding of RSSMRA85C01H501D: RSSMRA85C01H5LMG (place substitutions).
        Assert.Equal("RSSMRA85C01H5LMG", CodiceFiscale.Parse("RSSMRA85C01H5LMG").Valore);
    }

    [Fact]
    public void Parse_AcceptsOmocodiaAcrossYearDayAndPlacePositions()
    {
        // Published full-omocodia example in calcolo.tools' worked reference:
        // https://calcolo.tools/calcolatori/codice-fiscale-inverso/
        // RSSMRAURCLMHRLMN decodes to the base data encoded by RSSMRA85C01H501D.
        Assert.True(CodiceFiscale.IsValid("RSSMRAURCLMHRLMN"));
        Assert.Equal("RSSMRAURCLMHRLMN", CodiceFiscale.Parse("RSSMRAURCLMHRLMN").Valore);
    }

    [Fact]
    public void Parse_RejectsOmocodiaWithIncorrectControlCharacter()
    {
        Assert.False(CodiceFiscale.IsValid("RSSMRAURCLMHRLMO"));
        Assert.Throws<ArgumentException>(() => CodiceFiscale.Parse("RSSMRAURCLMHRLMO"));
    }

    [Theory]
    [InlineData("RSSMRAA5C01H501L")] // A is not a permitted omocodia substitute in year position 7.
    [InlineData("RSSMRA85C01H5A1D")] // A is not a permitted substitute in a numeric place-code position.
    public void Parse_RejectsLettersOutsideTheOmocodiaAlphabetInNumericPositions(string value)
    {
        Assert.False(CodiceFiscale.IsValid(value));
        Assert.Throws<ArgumentException>(() => CodiceFiscale.Parse(value));
    }

    [Theory]
    [InlineData("RSSMRA85C01H501A")]
    [InlineData("RSSMRA85C40H501D")]
    [InlineData("RSSMRA85Z01H501D")]
    [InlineData("RSSMRA85C01H50!D")]
    public void Parse_RejectsBadChecksumOrInvalidStructure(string value)
    {
        Assert.False(CodiceFiscale.IsValid(value));
        Assert.Throws<ArgumentException>(() => CodiceFiscale.Parse(value));
    }

    [Fact]
    public void GenerateItaliano_NormalizesDeterministicAccentsWithoutChangingInput()
    {
        const string nome = "José";
        const string cognome = "D'Ángelo";

        var value = CodiceFiscale.GenerateItaliano(nome, cognome, new DateOnly(1985, 12, 10), Sesso.M, "H501");

        Assert.Equal("DNGJSO85T10H501A", value.Valore);
        Assert.Equal("José", nome);
        Assert.Equal("D'Ángelo", cognome);
    }

    [Fact]
    public void GenerateItaliano_RejectsNonDecomposableLetterWithManageableDomainException()
    {
        var exception = Assert.Throws<CodiceFiscaleNonCalcolabileException>(() =>
            CodiceFiscale.GenerateItaliano("Łukasz", "Rossi", new DateOnly(1985, 12, 10), Sesso.M, "H501"));

        Assert.Contains("non possono essere normalizzate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateItaliano_RejectsOmocodiaCodeAsMunicipalityInput()
    {
        Assert.Throws<ArgumentException>(() => CodiceFiscale.GenerateItaliano(
            "Mario", "Rossi", new DateOnly(1985, 3, 1), Sesso.M, "H5LM"));
    }
}
