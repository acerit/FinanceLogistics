using System.Globalization;
using System.Text.Json;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.Imports;
namespace FinanceLogistics.Infrastructure.LogisticsNk;

public sealed record NkRawPayload(string Kind,PoCellDto[] Cells);
public static class NkSourcePolicy
{
    public static NkSourceProfile TemplateProfile(string source,string json)
    {
        using var d=JsonDocument.Parse(json); var r=d.RootElement;
        var p=NkSourceProfiles.Get(source,r.GetProperty("profile").GetString()??"");
        if(r.GetProperty("schemaVersion").GetInt32()!=p.SchemaVersion)
            throw new FormatException("Template schema does not match profile.");
        if(p.UsesCachedValues && (r.GetProperty("sheet").GetString()!=p.Sheet
            ||r.GetProperty("headerStart").GetInt32()!=1 ||r.GetProperty("headerEnd").GetInt32()!=p.HeaderRows.Length
            ||r.GetProperty("dataStart").GetInt32()!=p.DataStart ||r.GetProperty("formulaPolicy").GetString()!=p.FormulaPolicy))
            throw new FormatException("Template layout does not match compiled profile.");
        return p;
    }
    public static bool Supports(NkSourceProfile profile,string json)
    {
        try { return TemplateProfile(profile.SourceCode,json).SourceCode==profile.SourceCode; }
        catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or PoImportFault){return false;}
    }
    public static NkSourceProfile ProfileFromFrozen(string source,string options)
    {
        using var d=JsonDocument.Parse(options);
        return NkSourceProfiles.Get(source,d.RootElement.GetProperty("profile").GetString()??"");
    }
    public static NkParsePolicy Resolve(string json,DateOnly periodStart,DateOnly periodEnd,NkSourceProfile profile)
    {
        try
        {
            var limits=PoProfile.Limits(json);using var d=JsonDocument.Parse(json);var r=d.RootElement;
            if(r.GetProperty("coverageProperty").GetString()!=profile.CoverageProperty||r.GetProperty("dateTextCentury").GetInt32()!=2000)
                throw new FormatException();
            var mode=r.GetProperty("logisticsPeriodMode").GetString();
            var first=mode switch {
                "SELECTED_MONTH"=>periodStart,
                "YEAR_TO_PERIOD"=>new DateOnly(periodStart.Year,1,1),
                "EXPLICIT_RANGE"=>DateOnly.ParseExact(r.GetProperty("coverageStart").GetString()!,"yyyy-MM-dd",CultureInfo.InvariantCulture),
                _=>throw new FormatException()};
            var end=mode=="EXPLICIT_RANGE"?DateOnly.ParseExact(r.GetProperty("coverageEndExclusive").GetString()!,"yyyy-MM-dd",CultureInfo.InvariantCulture):periodEnd;
            if(end<=first||end.DayNumber-first.DayNumber>3660)throw new FormatException();
            return new(mode!,first,end,profile.UsesCachedValues,limits.Rows);
        }
        catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {throw new PoImportFault(409,"NK_PERIOD_POLICY_REQUIRED","Chưa cấu hình phạm vi file nhập khẩu, cột ngày và quy tắc năm. Không suy đoán từ tên file.");}
    }
    public static NkParsePolicy Frozen(string options,NkSourceProfile profile)
    {
        using var d=JsonDocument.Parse(options);var r=d.RootElement;
        if(r.GetProperty("sourceCode").GetString()!=profile.SourceCode||r.GetProperty("scopeCode").GetString()!=profile.ScopeCode
            ||r.GetProperty("profile").GetString()!=profile.Code||r.GetProperty("coverageProperty").GetString()!=profile.CoverageProperty
            ||r.GetProperty("dateTextCentury").GetInt32()!=2000
            ||TemplateProfile(profile.SourceCode,r.GetProperty("templateDefinition").GetRawText()).Code!=profile.Code
            ||(r.TryGetProperty("allowCachedAmountFormulas",out var cached)&&cached.GetBoolean())!=profile.UsesCachedValues
            ||profile.UsesCachedValues&&r.GetProperty("formulaPolicy").GetString()!=profile.FormulaPolicy)
            throw new InvalidOperationException("Frozen NK profile mismatch.");
        return new(r.GetProperty("periodMode").GetString()!,DateOnly.ParseExact(r.GetProperty("startDate").GetString()!,"yyyy-MM-dd",CultureInfo.InvariantCulture),
            DateOnly.ParseExact(r.GetProperty("endDateExclusive").GetString()!,"yyyy-MM-dd",CultureInfo.InvariantCulture),profile.UsesCachedValues,r.GetProperty("maxRows").GetInt32());
    }
}
