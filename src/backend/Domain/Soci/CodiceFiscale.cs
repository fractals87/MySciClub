namespace MySciClub.Domain.Soci;

public enum ProvenienzaCodiceFiscale
{
    Calcolato,
    Manuale
}

public sealed record CodiceFiscale
{
    private const string OmocodiaDigits = "LMNPQRSTUV";
    private static readonly int[] EvenDigitValues = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];
    private static readonly int[] OddDigitValues = [1, 0, 5, 7, 9, 13, 15, 17, 19, 21];
    private static readonly int[] EvenLetterValues = Enumerable.Range(0, 26).ToArray();
    private static readonly int[] OddLetterValues = [1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18, 20, 11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23];
    private static readonly int[] MonthDays = [31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    private const string MonthCodes = "ABCDEHLMPRST";

    public string Valore { get; }

    private CodiceFiscale(string valore) => Valore = valore;

    public static CodiceFiscale Parse(string valore)
    {
        var normalized = Normalize(valore);
        if (!IsValid(normalized)) throw new ArgumentException("Il codice fiscale non è formalmente valido.", nameof(valore));
        return new(normalized);
    }

    public static string Normalize(string valore)
    {
        ArgumentNullException.ThrowIfNull(valore);
        var normalized = valore.Trim().ToUpperInvariant();
        if (normalized.Length > 16) throw new ArgumentException("Il codice fiscale non può superare 16 caratteri.", nameof(valore));
        return normalized;
    }

    public static bool IsValid(string? value)
    {
        if (value is null) return false;
        var cf = value.Trim().ToUpperInvariant();
        if (cf.Length != 16) return false;
        if (!cf.Take(6).All(char.IsAsciiLetter) || !char.IsAsciiLetter(cf[8]) ||
            !char.IsAsciiLetter(cf[11]) || !cf.Skip(12).Take(3).All(IsDigitOrOmocodia) || !char.IsAsciiLetter(cf[15]))
            return false;
        if (!IsDigitOrOmocodia(cf[6]) || !IsDigitOrOmocodia(cf[7]) || !MonthCodes.Contains(cf[8]) || !IsDigitOrOmocodia(cf[9]) || !IsDigitOrOmocodia(cf[10]))
            return false;
        if (!IsValidDay(cf)) return false;

        var sum = 0;
        for (var index = 0; index < 15; index++)
        {
            var ch = cf[index];
            var oneBasedPosition = index + 1;
            sum += char.IsAsciiDigit(ch)
                ? (oneBasedPosition % 2 == 0 ? EvenDigitValues[ch - '0'] : OddDigitValues[ch - '0'])
                : (oneBasedPosition % 2 == 0 ? EvenLetterValues[ch - 'A'] : OddLetterValues[ch - 'A']);
        }
        return cf[15] == (char)('A' + sum % 26);
    }

    public static CodiceFiscale GenerateItaliano(string nome, string cognome, DateOnly dataNascita, Sesso sesso, string codiceCatastaleComune)
    {
        if (!Enum.IsDefined(sesso)) throw new ArgumentOutOfRangeException(nameof(sesso));
        var firstName = NormalizeName(nome, nameof(nome));
        var lastName = NormalizeName(cognome, nameof(cognome));
        var cadastral = Paese.NormalizeRequired(codiceCatastaleComune, 4, nameof(codiceCatastaleComune)).ToUpperInvariant();
        if (cadastral.Length != 4 || !char.IsAsciiLetter(cadastral[0]) || !cadastral.Skip(1).All(char.IsAsciiDigit))
            throw new ArgumentException("Il codice catastale del comune non è valido.", nameof(codiceCatastaleComune));

        var dateDay = dataNascita.Day + (sesso == Sesso.F ? 40 : 0);
        var first = EncodeName(firstName, isFirstName: true);
        var last = EncodeName(lastName, isFirstName: false);
        var body = string.Concat(
            last, first,
            (dataNascita.Year % 100).ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
            MonthCodes[dataNascita.Month - 1],
            dateDay.ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
            cadastral);
        var partial = body + 'A';
        var check = CalculateCheckCharacter(partial);
        return Parse(body + check);
    }

    private static char CalculateCheckCharacter(string first15)
    {
        var sum = 0;
        for (var index = 0; index < 15; index++)
        {
            var ch = first15[index];
            var position = index + 1;
            sum += char.IsAsciiDigit(ch)
                ? (position % 2 == 0 ? EvenDigitValues[ch - '0'] : OddDigitValues[ch - '0'])
                : (position % 2 == 0 ? EvenLetterValues[ch - 'A'] : OddLetterValues[ch - 'A']);
        }
        return (char)('A' + sum % 26);
    }

    private static bool IsDigitOrOmocodia(char value) => char.IsAsciiDigit(value) || OmocodiaDigits.Contains(value);

    private static bool IsValidDay(string cf)
    {
        var tens = DecodeDigit(cf[9]);
        var ones = DecodeDigit(cf[10]);
        var encodedDay = tens * 10 + ones;
        var day = encodedDay > 40 ? encodedDay - 40 : encodedDay;
        if (day < 1 || day > 31) return false;
        var month = MonthCodes.IndexOf(cf[8]);
        return day <= MonthDays[month];
    }

    private static int DecodeDigit(char value) => char.IsAsciiDigit(value) ? value - '0' : OmocodiaDigits.IndexOf(value);

    private static string NormalizeName(string value, string parameterName)
    {
        var valueWithAccents = Paese.NormalizeRequired(value, 100, parameterName).ToUpperInvariant();
        var decomposed = valueWithAccents.Normalize(System.Text.NormalizationForm.FormD);
        var normalized = new string(decomposed.Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
            .Normalize(System.Text.NormalizationForm.FormC);
        if (normalized.Any(ch => char.IsLetter(ch) && !char.IsAsciiLetter(ch)))
            throw new CodiceFiscaleNonCalcolabileException(
                "Il nome contiene lettere che non possono essere normalizzate in modo deterministico per il calcolo del codice fiscale.", parameterName);
        if (normalized.Any(ch => !char.IsAsciiLetter(ch) && ch != '\'' && ch != '’' && ch != '-' && !char.IsWhiteSpace(ch)))
            throw new ArgumentException("Il nome contiene caratteri non ammessi per il calcolo del codice fiscale.", parameterName);
        return normalized;
    }

    private static string EncodeName(string name, bool isFirstName)
    {
        var letters = name.Where(char.IsLetter).ToArray();
        var consonants = letters.Where(ch => !"AEIOU".Contains(ch)).ToArray();
        var vowels = letters.Where(ch => "AEIOU".Contains(ch)).ToArray();
        IEnumerable<char> selected = consonants;
        if (isFirstName && consonants.Length >= 4)
            selected = [consonants[0], consonants[2], consonants[3]];
        return new string(selected.Concat(vowels).Concat("XXX").Take(3).ToArray());
    }
}
