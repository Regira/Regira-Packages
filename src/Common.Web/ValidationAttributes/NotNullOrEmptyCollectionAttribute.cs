using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace Regira.Web.ValidationAttributes;

public class NotNullOrEmptyCollectionAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not ICollection { Count: > 0 } collection || !collection.GetEnumerator().MoveNext())
        {
            return new ValidationResult(ErrorMessage ?? "Collection should not be empty", [validationContext.MemberName
            ]!);
        }

        return ValidationResult.Success;
    }
}