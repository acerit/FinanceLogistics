using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Services;

namespace FinanceLogistics.Api.Web;

public static class CurrentUser
{
    public const string ItemKey = "FinanceLogistics.CurrentUser";
    public static UserProfile Get(HttpContext context) => context.Items[ItemKey] as UserProfile
        ?? throw IdentityFault.SessionExpired();
}
