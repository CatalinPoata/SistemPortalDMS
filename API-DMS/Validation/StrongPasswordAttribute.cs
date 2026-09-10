using System.ComponentModel.DataAnnotations;

namespace API_DMS.Validation
{
    public sealed class StrongPasswordAttribute
    : ValidationAttribute
    {
        protected override ValidationResult? IsValid(
            object? value,
            ValidationContext validationContext)
        {
            if (value is not string password ||
                string.IsNullOrWhiteSpace(password))
            {
                return ValidationResult.Success;
            }

            if (password.Length < 10)
            {
                return new ValidationResult(
                    "Parola trebuie să aibă cel puțin 10 caractere.");
            }

            var classes = 0;

            if (password.Any(char.IsUpper))
            {
                classes++;
            }

            if (password.Any(char.IsLower))
            {
                classes++;
            }

            if (password.Any(char.IsDigit))
            {
                classes++;
            }

            if (password.Any(character =>
                    !char.IsLetterOrDigit(character)))
            {
                classes++;
            }

            if (classes < 3)
            {
                return new ValidationResult(
                    "Parola trebuie să conțină cel puțin trei clase de caractere.");
            }

            return ValidationResult.Success;
        }
    }
}
