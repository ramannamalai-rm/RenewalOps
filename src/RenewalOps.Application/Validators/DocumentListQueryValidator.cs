using FluentValidation;
using RenewalOps.Application.DTOs.Documents;

namespace RenewalOps.Application.Validators;

public sealed class DocumentListQueryValidator : AbstractValidator<DocumentListQuery>
{
    public DocumentListQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.ExpiringWithinDays)
            .GreaterThanOrEqualTo(1)
            .When(x => x.ExpiringWithinDays.HasValue);
    }
}
