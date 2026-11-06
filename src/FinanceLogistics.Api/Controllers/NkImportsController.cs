using System.Globalization;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Application.LogisticsNk;
using FinanceLogistics.Api.Repositories;
using FinanceLogistics.Api.Security;
using FinanceLogistics.Api.Services;
using FinanceLogistics.Api.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FinanceLogistics.Api.Controllers;

[ApiController]
[Authorize]
[TypeFilter(typeof(PoImportExceptionFilter))]
[Route("api/logistics/nk/{sourceCode}")]
public sealed class NkImportsController(INkImportFactory factory,IIdentityStore identityStore):ControllerBase
{
    private string SourceCode=>Convert.ToString(RouteData.Values["sourceCode"],CultureInfo.InvariantCulture)??"";
    private string ScopeCode=>SourceCode switch { "NK_1000"=>"SITE_1000", "NK_6000"=>"SITE_6000", _=>throw new PoImportFault(404,"NK_SOURCE_NOT_FOUND","Không có nguồn nhập khẩu này.") };
    [HttpGet("/api/logistics/nk/sources")]
    public async Task<IActionResult> Sources(CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        var user=CurrentUser.Get(HttpContext);
        var sources=(await identityStore.SourcesAsync(ct)).Where(x=>
            (x.SourceCode=="NK_1000"&&x.ScopeCode=="SITE_1000"&&x.Sites.SequenceEqual(new[]{"1000"})
             ||x.SourceCode=="NK_6000"&&x.ScopeCode=="SITE_6000"&&x.Sites.SequenceEqual(new[]{"6000"}))
            &&x.OwnerRole==AccessRules.Logistics&&AccessRules.CanAccessSource(user,x));
        return Ok(sources.Select(x=>new {x.SourceCode,x.ScopeCode,x.Sites}));
    }
    private INkSourceImportService Imports=>factory.For(SourceCode);
    [HttpGet("periods")]
    public async Task<IActionResult> Periods(CancellationToken ct)
    {await RequireAsync("read",ct);return Ok(await Imports.PeriodsAsync(ct));}
    [HttpGet("periods/{periodId:int}/requirements")]
    public async Task<IActionResult> Requirements(int periodId,CancellationToken ct)
    {await RequireAsync("read",ct);return Ok(await Imports.RequirementsAsync(periodId,ct));}
    [HttpGet("requirements/{requirementId:long}/policy")]
    public async Task<IActionResult> RequirementPolicy(long requirementId,CancellationToken ct)
    {await RequireAsync("read",ct);return Ok(await Imports.RequirementPolicyAsync(requirementId,ct));}
    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct)
    { await RequireAsync("read", ct); return Ok(await Imports.TemplatesAsync(ct)); }

    [HttpGet("templates/{templateVersionId:long}/file")]
    public async Task<IActionResult> BlankTemplate(long templateVersionId,CancellationToken ct)
    {
        await RequireAsync("read",ct);
        var id=templateVersionId.ToString(CultureInfo.InvariantCulture);
        var template=(await Imports.TemplatesAsync(ct)).SingleOrDefault(t=>t.TemplateVersionId==id);
        if(template is null)throw new PoImportFault(404,"NK_TEMPLATE_NOT_FOUND","Mẫu không thuộc nguồn/site hoặc chưa được duyệt.");
        var selected=FinanceLogistics.Infrastructure.LogisticsNk.NkSourceProfiles.Get(SourceCode,template.Profile);
        if(selected.SchemaVersion!=4)
            throw new PoImportFault(409,"NK_TEMPLATE_LEGACY","Mẫu cũ không có file tải mới; hãy chọn HEADER1_V4 hoặc dùng file nguồn gốc theo profile cũ.");
        var payload=FinanceLogistics.Api.Assets.HeaderRowOneXlsx.Build(selected.Sheet,selected.Fields.Select(f=>(f.Column,f.Header)));
        Response.Headers.CacheControl="no-store";
        return File(payload,"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",$"{SourceCode}_template_{template.VersionNo}.xlsx");
    }

    [HttpPost("requirements/{requirementId:long}/uploads")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(long requirementId, [FromForm] PoUploadForm form, CancellationToken ct)
    {
        var actor = await RequireAsync("upload", ct);
        if (form.File is null) throw new PoImportFault(400, "FILE_REQUIRED", "Chưa chọn file.");
        // New uploads must use a newly-approved single-header-row V4 template.
        // Older V1-V3 profiles stay compiled for history, original download, and
        // frozen-batch inspection, but cannot accept a new upload through HTTP.
        var chosenId=Convert.ToString(form.TemplateVersionId,CultureInfo.InvariantCulture);
        var chosen=(await Imports.TemplatesAsync(ct)).SingleOrDefault(t=>t.TemplateVersionId==chosenId);
        if(chosen is null)throw new PoImportFault(404,"TEMPLATE_NOT_FOUND","Mẫu không được duyệt cho nguồn/site này.");
        if(!chosen.Profile.EndsWith("_HEADER1_V4",StringComparison.Ordinal))
            throw new PoImportFault(409,"HEADER_ROW_ONE_REQUIRED","File nộp mới phải dùng template V4: header hàng 1, dữ liệu từ hàng 2.");
        await using var stream = form.File.OpenReadStream();
        var result = await Imports.UploadAsync(new(requirementId, form.TemplateVersionId,
            form.ClientUploadId, actor, Correlation(), form.File.FileName), stream, ct);
        return Accepted($"/api/logistics/nk/{SourceCode}/batches/{result.BatchId}", result);
    }
    [HttpGet("requirements/{requirementId:long}/history")]
    public async Task<IActionResult> History(long requirementId, [FromQuery] int page = 1, CancellationToken ct = default)
    { await RequireAsync("read", ct); return Ok(await Imports.HistoryAsync(requirementId, page, ct)); }
    [HttpGet("batches/{batchId:long}")]
    public async Task<IActionResult> Batch(long batchId, CancellationToken ct)
    { await RequireAsync("read", ct); return Ok(await Imports.GetBatchAsync(batchId, ct)); }
    [HttpGet("batches/{batchId:long}/rows")]
    public async Task<IActionResult> Rows(long batchId, [FromQuery] int page = 1, CancellationToken ct = default)
    { await RequireAsync("read", ct); return Ok(await Imports.RowsAsync(batchId, page, ct)); }
    [HttpGet("batches/{batchId:long}/issues")]
    public async Task<IActionResult> Issues(long batchId, [FromQuery] int page = 1, CancellationToken ct = default, [FromQuery] string? issueCode = null)
    { await RequireAsync("read", ct); return Ok(await Imports.IssuesAsync(batchId, page, ct, issueCode)); }
    [HttpGet("batches/{batchId:long}/issues/summary")]
    public async Task<IActionResult> IssueSummary(long batchId,CancellationToken ct)
    {await RequireAsync("read",ct);return Ok(await Imports.IssueSummaryAsync(batchId,ct));}
    [HttpGet("batches/{batchId:long}/original")]
    public async Task<IActionResult> Original(long batchId, CancellationToken ct)
    {
        await RequireAsync("read", ct);
        var file = await Imports.OpenOriginalAsync(batchId, ct);
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
        var result = await Imports.ConfirmAtomicallyAsync(new(batchId, actor, expected, body.AcceptWarnings, Correlation()), ct);
        return Ok(new { batchId = result.BatchId.ToString(CultureInfo.InvariantCulture),
            requirementId = result.RequirementId.ToString(CultureInfo.InvariantCulture), result.AlreadyCurrent });
    }
    [HttpPost("batches/{batchId:long}/retry-validation")]
    public async Task<IActionResult> Retry(long batchId, CancellationToken ct)
    {
        var actor = await RequireAsync("upload", ct);
        await Imports.RetryValidationAsync(batchId, actor, Correlation(), ct);
        return Accepted($"/api/logistics/nk/{SourceCode}/batches/{batchId}", new { batchId = batchId.ToString(CultureInfo.InvariantCulture), status = "QUEUED" });
    }
    private Guid Correlation() => Guid.TryParse(HttpContext.TraceIdentifier, out var parsed) ? parsed : Guid.NewGuid();
    private async Task<long> RequireAsync(string operation, CancellationToken ct)
    {
        _ = factory.For(SourceCode);
        Response.Headers.CacheControl = "no-store";
        var source = (await identityStore.SourcesAsync(ct)).SingleOrDefault(x =>
            x.SourceCode == SourceCode && x.ScopeCode == ScopeCode);
        var user = CurrentUser.Get(HttpContext);
        if (source is null || source.OwnerRole != AccessRules.Logistics || !source.Sites.SequenceEqual(new[]{ScopeCode[5..]}) || !AccessRules.CanAccessSource(user, source, operation))
            throw new IdentityFault(403, "SOURCE_FORBIDDEN", "Bạn không có quyền thực hiện thao tác với nguồn nhập khẩu này.");
        return long.Parse(user.UserId, CultureInfo.InvariantCulture);
    }
}
