using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroMax.Server.Pages;

[AllowAnonymous]
public sealed class RegisterSuccessModel : PageModel
{
    public void OnGet()
    {
    }
}
