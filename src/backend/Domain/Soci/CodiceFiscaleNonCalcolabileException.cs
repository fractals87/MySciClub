namespace MySciClub.Domain.Soci;

public sealed class CodiceFiscaleNonCalcolabileException(string message, string parameterName)
    : ArgumentException(message, parameterName);
