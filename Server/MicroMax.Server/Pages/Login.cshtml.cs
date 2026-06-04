using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroMax.Server.Pages;

[AllowAnonymous]
public sealed class LoginModel(AdminPanelSignInService signInService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(GetSafeReturnUrl());
        }

        ReturnUrl = GetSafeReturnUrl();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ReturnUrl = GetSafeReturnUrl();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await signInService.ValidateCredentialsAsync(Input.Email, Input.Password, cancellationToken);
        if (user is null)
        {
            ErrorMessage = "Неверные данные входа или недостаточно прав для доступа к панели.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email)
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, AdminPanelAuthenticationDefaults.Scheme));

        await HttpContext.SignInAsync(
            AdminPanelAuthenticationDefaults.Scheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = Input.RememberMe,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(Input.RememberMe ? 24 : 8)
            });

        return LocalRedirect(ReturnUrl!);
    }

    private string GetSafeReturnUrl()
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return ReturnUrl;
        }

        return Url.Page("/Index") ?? "/";
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Укажите email.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите пароль.")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
