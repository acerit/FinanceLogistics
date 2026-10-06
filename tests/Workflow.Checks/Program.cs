using FinanceLogistics.Domain.Imports;

var valid = new ConfirmFacts(PeriodStatus.Open, BatchStatus.Ready, false, true,
    0, 0, false, true, 2, true);
var cases = new (string Name, ConfirmFacts Facts, ConfirmDecision Decision, string? Code)[]
{
    ("Ready data can be confirmed", valid, ConfirmDecision.Apply, null),
    ("Closed period", valid with { PeriodStatus = PeriodStatus.Closed }, ConfirmDecision.Reject, "PERIOD_NOT_OPEN"),
    ("Review period", valid with { PeriodStatus = PeriodStatus.Review }, ConfirmDecision.Reject, "PERIOD_NOT_OPEN"),
    ("Another user changed active source", valid with { ExpectedVersionMatches = false }, ConfirmDecision.Reject, "REQUIREMENT_CHANGED"),
    ("Validation still running", valid with { BatchStatus = BatchStatus.Validating }, ConfirmDecision.Reject, "BATCH_NOT_READY"),
    ("Validation errors", valid with { ErrorCount = 1 }, ConfirmDecision.Reject, "BATCH_NOT_READY"),
    ("Delta is not supported", valid with { IsSnapshot = false }, ConfirmDecision.Reject, "UNSUPPORTED_INGESTION_MODE"),
    ("Empty file cannot erase source", valid with { TotalRows = 0 }, ConfirmDecision.Reject, "EMPTY_DATA_BATCH"),
    ("Draft template", valid with { TemplateApproved = false }, ConfirmDecision.Reject, "TEMPLATE_NOT_APPROVED"),
    ("Warnings need acknowledgement", valid with { WarningCount = 1 }, ConfirmDecision.Reject, "WARNINGS_NOT_ACCEPTED"),
    ("Acknowledged warning", valid with { WarningCount = 1, WarningsAccepted = true }, ConfirmDecision.Apply, null),
    ("Lost response retry", valid with { BatchStatus = BatchStatus.Committed, IsCurrent = true, ExpectedVersionMatches = false }, ConfirmDecision.AlreadyCurrent, null),
    ("Old batch must not become active by retry", valid with { BatchStatus = BatchStatus.Committed }, ConfirmDecision.Reject, "BATCH_SUPERSEDED")
};
foreach (var c in cases)
{
    var actual = ConfirmPolicy.Evaluate(c.Facts);
    if (actual.Decision != c.Decision || actual.ErrorCode != c.Code)
        throw new Exception($"FAIL: {c.Name}; actual={actual}");
    Console.WriteLine($"PASS: {c.Name}");
}
Console.WriteLine($"{cases.Length} rule checks passed. SQL/AD/concurrency integration still requires Windows and SQL Server.");
