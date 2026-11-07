using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MySciClub.Application.Abstractions.Audit;
using MySciClub.Application.Features.Members;
using MySciClub.Infrastructure.Persistence.Entities;

namespace MySciClub.Infrastructure.Persistence;

internal sealed class SqlNumeroSocioGenerator(MySciClubDbContext db) : INumeroSocioGenerator
{
    public async Task<long> GetNextAsync(CancellationToken cancellationToken = default)
    {
        var values = await db.Database.SqlQueryRaw<long>("SELECT NEXT VALUE FOR [dbo].[SeqNumeroSocio] AS [Value]")
            .ToListAsync(cancellationToken);
        return values.Single();
    }
}

internal sealed class SqlAuditService(MySciClubDbContext db) : IAuditService
{
    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        db.AuditEntries.Add(new AuditRecord
        {
            AdministratorId = entry.AdministratorId,
            RecordedAtUtc = entry.RecordedAtUtc.ToUniversalTime(),
            EffectiveDate = entry.EffectiveDate,
            Operation = entry.Operation,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            PreviousValues = entry.PreviousValues,
            NewValues = entry.NewValues
        });
        return Task.CompletedTask;
    }
}

internal sealed class SqlApplicationUnitOfWork(MySciClubDbContext db) : IApplicationUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return result;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await RollbackAndClearAsync(transaction);
            throw new SocioApplicationException(SocioErrorCode.ConflittoAggiornamento, "Il socio è stato modificato da un'altra operazione.", exception);
        }
        catch (DbUpdateException exception)
        {
            await RollbackAndClearAsync(transaction);
            throw Translate(exception);
        }
        catch
        {
            await RollbackAndClearAsync(transaction);
            throw;
        }
    }

    private async Task RollbackAndClearAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try { await transaction.RollbackAsync(CancellationToken.None); }
        finally { db.ChangeTracker.Clear(); }
    }

    private static Exception Translate(DbUpdateException exception)
    {
        if (exception.GetBaseException() is SqlException { Number: 2601 or 2627 } sql)
        {
            if (sql.Message.Contains("CodiceFiscale", StringComparison.OrdinalIgnoreCase))
                return new SocioApplicationException(SocioErrorCode.CodiceFiscaleDuplicato, "Il codice fiscale è già associato a un socio.", exception);
            if (sql.Message.Contains("NumeroSocio", StringComparison.OrdinalIgnoreCase))
                return new SocioApplicationException(SocioErrorCode.NumeroSocioNonAssegnabile, "Il numero socio non è assegnabile.", exception);
        }
        return exception;
    }
}

/// <summary>Current schema has no activity or purchase relations; expand this adapter as those modules add persisted links.</summary>
internal sealed class CurrentSchemaSocioRelationChecker : ISocioRelationChecker
{
    public Task<IReadOnlyList<string>> GetDeletionBlockersAsync(long socioId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
