namespace FinanceLogistics.Domain.Imports;

public enum BatchStatus { Received, Validating, Ready, Invalid, Committed, Discarded }
public enum PeriodStatus { Open, Review, Closed }
public enum ConfirmDecision { Apply, AlreadyCurrent, Reject }
public sealed record ConfirmFacts(
    PeriodStatus PeriodStatus, BatchStatus BatchStatus,
    bool IsCurrent, bool ExpectedVersionMatches,
    int ErrorCount, int WarningCount, bool WarningsAccepted,
    bool IsSnapshot, int TotalRows, bool TemplateApproved);
public sealed record ConfirmEvaluation(ConfirmDecision Decision, string? ErrorCode = null);

// Pure rules for a DATA batch in the first PO milestone.
// Authorization, persisted facts and locking belong to Application/Infrastructure.
public static class ConfirmPolicy
{
    public static ConfirmEvaluation Evaluate(ConfirmFacts f)
    {
        // An HTTP retry of an already completed operation must not reactivate an old version.
        if (f.BatchStatus == BatchStatus.Committed)
            return f.IsCurrent ? new(ConfirmDecision.AlreadyCurrent)
                : Reject("BATCH_SUPERSEDED");
        if (f.PeriodStatus != PeriodStatus.Open) return Reject("PERIOD_NOT_OPEN");
        if (!f.ExpectedVersionMatches) return Reject("REQUIREMENT_CHANGED");
        if (f.BatchStatus != BatchStatus.Ready || f.ErrorCount != 0) return Reject("BATCH_NOT_READY");
        if (!f.IsSnapshot) return Reject("UNSUPPORTED_INGESTION_MODE");
        if (f.TotalRows <= 0) return Reject("EMPTY_DATA_BATCH");
        if (!f.TemplateApproved) return Reject("TEMPLATE_NOT_APPROVED");
        if (f.WarningCount > 0 && !f.WarningsAccepted) return Reject("WARNINGS_NOT_ACCEPTED");
        return new(ConfirmDecision.Apply);
    }
    private static ConfirmEvaluation Reject(string code) => new(ConfirmDecision.Reject, code);
}
