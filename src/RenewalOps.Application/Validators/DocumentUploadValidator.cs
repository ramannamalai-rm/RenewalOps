using FluentValidation;
using RenewalOps.Application.DTOs.Documents;

namespace RenewalOps.Application.Validators;

public sealed class DocumentUploadValidator : AbstractValidator<UploadDocumentCommand>
{
    private static readonly string[] AllowedContentTypes =
    [
        "application/pdf",
        "image/jpeg",
        "image/png"
    ];

    public DocumentUploadValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.FileName)
            .NotEmpty();

        RuleFor(x => x.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage($"Content type must be one of: {string.Join(", ", AllowedContentTypes)}.");

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(52_428_800)
            .WithMessage("File size must not exceed 50 MB.");
    }
}
