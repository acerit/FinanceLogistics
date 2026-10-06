namespace FinanceLogistics.Application.Imports;

// This contract describes the next implementation; no SQL implementation is registered yet.
// CurrentActorId MUST come from the authenticated server-side user, never a browser form.
public sealed record ConfirmPoCommand(long BatchId, long CurrentActorId,
    byte[] ExpectedRequirementRowVersion, bool AcceptWarnings, Guid CorrelationId);
public sealed record ConfirmPoResult(long BatchId, long RequirementId, bool AlreadyCurrent);

public interface IPoConfirmationStore
{
    // One transaction: lock period -> requirement -> batch; verify current permission,
    // persisted validation facts and expected rowversion; update pointer + revision + audit.
    // Return success for a retry only if the same batch is still current.
    Task<ConfirmPoResult> ConfirmAtomicallyAsync(ConfirmPoCommand command, CancellationToken ct);
}

public sealed class ConfirmPoImport(IPoConfirmationStore store)
{
    public Task<ConfirmPoResult> ExecuteAsync(ConfirmPoCommand command, CancellationToken ct)
    {
        if (command.BatchId <= 0 || command.CurrentActorId <= 0)
            throw new ArgumentException("Valid batch and authenticated actor are required.");
        if (command.ExpectedRequirementRowVersion is not { Length: 8 })
            throw new ArgumentException("A SQL Server rowversion must contain 8 bytes.");
        return store.ConfirmAtomicallyAsync(command, ct);
    }
}

public interface IFileStorage
{
    Task<StoredObject> SaveAsync(Stream content, string displayName, long maxBytes, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
}
public sealed record StoredObject(string StorageKey, long SizeBytes, byte[] Sha256);

public interface IImportJobProcessor
{
    // SQL job lease/fencing is implemented by the Infrastructure adapter, not by this interface.
    Task<bool> TryProcessNextAsync(CancellationToken ct);
}
