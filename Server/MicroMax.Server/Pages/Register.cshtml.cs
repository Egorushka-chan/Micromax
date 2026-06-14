using System.ComponentModel.DataAnnotations;
using MicroMax.Server.Infrastructure.Api;
using MicroMax.Server.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroMax.Server.Pages;

[AllowAnonymous]
public sealed class RegisterModel(UserAccountService userAccountService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Admin");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            await userAccountService.CreateAsync(
                new CreateUserAccountRequest(
                    Input.Email,
                    Input.Password,
                    Input.DisplayName,
                    CanAccessWebPanel: true),
                cancellationToken);

            return RedirectToPage("/RegisterSuccess");
        }
        catch (ApiValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
        catch (ApiConflictException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return Page();
        }
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Укажите имя пользователя.")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите email.")]
        [EmailAddress(ErrorMessage = "Укажите корректный email.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Укажите пароль.")]
        [MinLength(8, ErrorMessage = "Пароль должен содержать не менее 8 символов.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Подтвердите пароль.")]
        [Compare(nameof(Password), ErrorMessage = "Пароли должны совпадать.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
