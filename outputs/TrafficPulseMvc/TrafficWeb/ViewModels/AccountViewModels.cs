using System.ComponentModel.DataAnnotations;
namespace TrafficWeb.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "יש להזין דואר אלקטרוני."), EmailAddress(ErrorMessage = "כתובת לא תקינה."), StringLength(254)]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "יש להזין סיסמה."), StringLength(128), DataType(DataType.Password)]
    public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public class RegisterViewModel
{
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password), ErrorMessage = "הסיסמאות אינן תואמות."), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}
