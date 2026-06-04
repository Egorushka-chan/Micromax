using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroMax.Server.Pages;

[Authorize(AuthenticationSchemes = AdminPanelAuthenticationDefaults.Scheme)]
[IgnoreAntiforgeryToken]
public sealed class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(AdminPanelAuthenticationDefaults.Scheme);
        return RedirectToPage("/Login");
    }
}
