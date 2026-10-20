using System.Globalization;
using System.Text.Json;
using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.Imports;
namespace FinanceLogistics.Infrastructure.FinanceSources;

public sealed record FinanceRawPayload(string Kind,PoCellDto[] Cells);
public static class FinanceSourcePolicy
{
    public static bool Supports(FinanceSourceProfile profile,string json)
    {
        try { using var d=JsonDocument.Parse(json);return d.RootElement.GetProperty("profile").GetString()==profile.Code
            &&d.RootElement.GetProperty("schemaVersion").GetInt32()==1; }
        catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException){return false;}
    }
    public static FinanceParsePolicy Resolve(string json,DateOnly start,DateOnly end)
    {
        try
        {
            var limits=PoProfile.Limits(json);using var d=JsonDocument.Parse(json);var r=d.RootElement;
            var mode=r.GetProperty("financePeriodMode").GetString();
            var rangeStart=mode switch {"SELECTED_MONTH"=>start,"YEAR_TO_PERIOD"=>new DateOnly(start.Year,1,1),
                "EXPLICIT_RANGE"=>DateOnly.ParseExact(r.GetProperty("coverageStart").GetString()!,"yyyy-MM-dd",CultureInfo.InvariantCulture),_=>throw new FormatException()};
            var rangeEnd=mode=="EXPLICIT_RANGE"?DateOnly.ParseExact(r.GetProperty("coverageEndExclusive").GetString()!,"yyyy-MM-dd",CultureInfo.InvariantCulture):end;
            if(rangeEnd<=rangeStart||rangeEnd.DayNumber-rangeStart.DayNumber>3660)throw new FormatException();
            var formulas=r.TryGetProperty("allowCachedAmountFormulas",out var f)&&f.GetBoolean();
            return new(mode!,rangeStart,rangeEnd,formulas,limits.Rows);
        }
        catch(Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {throw new PoImportFault(409,"FINANCE_PERIOD_POLICY_REQUIRED","Chưa cấu hình cách nhận dữ liệu Finance theo tháng/lũy kế/phạm vi cụ thể. Không tự suy đoán từ tên file.");}
    }
    public static FinanceParsePolicy Frozen(string options,FinanceSourceProfile profile)
    {
        using var d=JsonDocument.Parse(options);var r=d.RootElement;
        if(r.GetProperty("sourceCode").GetString()!=profile.SourceCode||r.GetProperty("profile").GetString()!=profile.Code
            ||!Supports(profile,r.GetProperty("templateDefinition").GetRawText()))throw new InvalidOperationException("Frozen profile mismatch.");
        return new(r.GetProperty("periodMode").GetString()!,DateOnly.ParseExact(r.GetProperty("startDate").GetString()!,"yyyy-MM-dd",CultureInfo.InvariantCulture),
            DateOnly.ParseExact(r.GetProperty("endDateExclusive").GetString()!,"yyyy-MM-dd",CultureInfo.InvariantCulture),
            r.GetProperty("allowCachedAmountFormulas").GetBoolean(),r.GetProperty("maxRows").GetInt32());
    }
}
