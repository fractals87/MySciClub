using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using MySciClub.Application.Features.Members;
using MySciClub.Infrastructure.Persistence;

namespace MySciClub.Api.ApiSecurity;

public sealed class SocioExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is TesseramentoUnavailableException)
        {
            await WriteProblemAsync(context, StatusCodes.Status503ServiceUnavailable,
                "Tesseramento non disponibile", "Lo stato di tesseramento non è ancora disponibile.", "tesseramento_non_disponibile", cancellationToken);
            return true;
        }
        if (exception is not SocioApplicationException applicationException) return false;

        var (status, title) = applicationException.Code switch
        {
            SocioErrorCode.DatiNonValidi => (StatusCodes.Status400BadRequest, "Dati non validi"),
            SocioErrorCode.SocioNonTrovato or SocioErrorCode.PaeseNonTrovato or SocioErrorCode.ComuneNonTrovato => (StatusCodes.Status404NotFound, "Risorsa non trovata"),
            SocioErrorCode.GeografiaNonCoerente => (StatusCodes.Status400BadRequest, "Combinazione geografica non valida"),
            SocioErrorCode.EliminazioneBloccata or SocioErrorCode.CodiceFiscaleDuplicato or SocioErrorCode.ConflittoAggiornamento => (StatusCodes.Status409Conflict, "Operazione in conflitto"),
            SocioErrorCode.NonAutorizzato => (StatusCodes.Status403Forbidden, "Operazione non autorizzata"),
            SocioErrorCode.NumeroSocioNonAssegnabile or SocioErrorCode.ErroreStorage => (StatusCodes.Status503ServiceUnavailable, "Servizio temporaneamente non disponibile"),
            SocioErrorCode.ErroreElaborazioneFoto => (StatusCodes.Status400BadRequest, "Fotografia non valida"),
            SocioErrorCode.ErroreEsportazione => (StatusCodes.Status500InternalServerError, "Esportazione non riuscita"),
            _ => (StatusCodes.Status500InternalServerError, "Errore applicativo")
        };
        await WriteProblemAsync(context, status, title, applicationException.Message, applicationException.Code.ToString(), cancellationToken);
        return true;
    }

    private static Task WriteProblemAsync(HttpContext context, int status, string title, string detail, string code, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{status}"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.ContentType = "application/problem+json";
        return JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: cancellationToken);
    }
}
