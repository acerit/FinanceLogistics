using System.Globalization;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Api.Repositories;
using FinanceLogistics.Api.Security;
using FinanceLogistics.Api.Services;
using FinanceLogistics.Api.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FinanceLogistics.Api.Controllers;

[ApiController]
[Authorize(Policy = "FinanceBusiness")]
[TypeFilter(typeof(PoImportExceptionFilter))]
[Route("api/po")]
public sealed class PoImportsController(IPoImportService imports, ConfirmPoImport confirm,
    IIdentityStore identityStore) : ControllerBase
{
    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct)
    { await RequireAsync("read", ct); return Ok(await imports.TemplatesAsync(ct)); }

    [HttpPost("requirements/{requirementId:long}/uploads")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(long requirementId, [FromForm] PoUploadForm form, CancellationToken ct)
    {
        var actor = await RequireAsync("upload", ct);
        if (form.File is null) throw new PoImportFault(400, "FILE_REQUIRED", "Chưa chọn file.");
        await using var stream = form.File.OpenReadStream();
        var result = await imports.UploadAsync(new(requirementId, form.TemplateVersionId,
            form.ClientUploadId, actor, Correlation(), form.File.FileName), stream, ct);
        return Accepted($"/api/po/batches/{result.BatchId}", result);
    }
    [HttpGet("requirements/{requirementId:long}/history")]
    public async Task<IActionResult> History(long requirementId, [FromQuery] int page = 1, CancellationToken ct = default)
    { await RequireAsync("read", ct); return Ok(await imports.HistoryAsync(requirementId, page, ct)); }
    [HttpGet("batches/{batchId:long}")]
    public async Task<IActionResult> Batch(long batchId, CancellationToken ct)
    { await RequireAsync("read", ct); return Ok(await imports.GetBatchAsync(batchId, ct)); }
    [HttpGet("batches/{batchId:long}/rows")]
    public async Task<IActionResult> Rows(long batchId, [FromQuery] int page = 1, CancellationToken ct = default)
    { await RequireAsync("read", ct); return Ok(await imports.RowsAsync(batchId, page, ct)); }
    [HttpGet("batches/{batchId:long}/issues")]
    public async Task<IActionResult> Issues(long batchId, [FromQuery] int page = 1, CancellationToken ct = default)
    { await RequireAsync("read", ct); return Ok(await imports.IssuesAsync(batchId, page, ct)); }
    [HttpGet("batches/{batchId:long}/original")]
    public async Task<IActionResult> Original(long batchId, CancellationToken ct)
    {
        await RequireAsync("read", ct);
        var file = await imports.OpenOriginalAsync(batchId, ct);
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Content, "application/octet-stream", file.OriginalName, enableRangeProcessing: false);
    }
    [HttpPost("batches/{batchId:long}/confirm")]
    public async Task<IActionResult> Confirm(long batchId, [FromBody] PoConfirmBody body, CancellationToken ct)
    {
        var actor = await RequireAsync("confirm", ct);
        byte[] expected;
        try { expected = Convert.FromBase64String(body.ExpectedRequirementRowVersion ?? ""); }
        catch (FormatException) { throw new PoImportFault(400, "ROWVERSION_REQUIRED", "Mã phiên bản preview không hợp lệ."); }
        if (expected.Length != 8) throw new PoImportFault(400, "ROWVERSION_REQUIRED", "Cần mã phiên bản lấy từ preview.");
        var result = await confirm.ExecuteAsync(new(batchId, actor, expected, body.AcceptWarnings, Correlation()), ct);
        return Ok(new { batchId = result.BatchId.ToString(CultureInfo.InvariantCulture),
            requirementId = result.RequirementId.ToString(CultureInfo.InvariantCulture), result.AlreadyCurrent });
    }
    [HttpPost("batches/{batchId:long}/retry-validation")]
    public async Task<IActionResult> Retry(long batchId, CancellationToken ct)
    {
        var actor = await RequireAsync("upload", ct);
        await imports.RetryValidationAsync(batchId, actor, Correlation(), ct);
        return Accepted($"/api/po/batches/{batchId}", new { batchId = batchId.ToString(CultureInfo.InvariantCulture), status = "QUEUED" });
    }
    private Guid Correlation() => Guid.TryParse(HttpContext.TraceIdentifier, out var parsed) ? parsed : Guid.NewGuid();
    private async Task<long> RequireAsync(string operation, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var source = (await identityStore.SourcesAsync(ct)).SingleOrDefault(x =>
            x.SourceCode == "PO_UPDATED" && x.ScopeCode == "FINANCE_SHARED");
        var user = CurrentUser.Get(HttpContext);
        if (source is null || !AccessRules.CanAccessSource(user, source, operation))
            throw new IdentityFault(403, "SOURCE_FORBIDDEN", "Bạn không có quyền thực hiện thao tác với PO_UPDATED.");
        return long.Parse(user.UserId, CultureInfo.InvariantCulture);
    }
}

public sealed class PoUploadForm
{
    public IFormFile? File { get; set; }
    public long TemplateVersionId { get; set; }
    public Guid ClientUploadId { get; set; }
}
public sealed record PoConfirmBody(string? ExpectedRequirementRowVersion, bool AcceptWarnings);

public sealed class PoImportExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not PoImportFault fault) return;
        context.Result = new ObjectResult(new { code = fault.Code, message = fault.Message,
            correlationId = context.HttpContext.TraceIdentifier }) { StatusCode = fault.Status };
        context.ExceptionHandled = true;
    }
}
