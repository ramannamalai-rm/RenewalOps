using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RenewalOps.Application.DTOs.Documents;
using RenewalOps.Application.Interfaces;
using RenewalOps.Domain.Enums;

namespace RenewalOps.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IValidator<UploadDocumentCommand> _uploadValidator;
    private readonly IValidator<DocumentListQuery> _listValidator;

    public DocumentsController(
        IDocumentService documentService,
        IValidator<UploadDocumentCommand> uploadValidator,
        IValidator<DocumentListQuery> listValidator)
    {
        _documentService = documentService;
        _uploadValidator = uploadValidator;
        _listValidator = listValidator;
    }

    /// <summary>Upload a new document with file attachment.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromForm] string title,
        [FromForm] string documentType,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "File is required." });

        if (!Enum.TryParse<DocumentType>(documentType, ignoreCase: true, out var docType))
            return BadRequest(new { error = $"Invalid document type '{documentType}'. Valid values: {string.Join(", ", Enum.GetNames<DocumentType>())}" });

        var command = new UploadDocumentCommand
        {
            Title = title,
            DocumentType = docType,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length
        };

        var validation = await _uploadValidator.ValidateAsync(command, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        var result = await _documentService.UploadAsync(GetUserId(), command, file.OpenReadStream(), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>List documents with optional filtering and paging.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(DocumentListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] DocumentListQuery query, CancellationToken ct)
    {
        var validation = await _listValidator.ValidateAsync(query, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        var result = await _documentService.ListAsync(GetUserId(), GetUserRole(), query, ct);
        return Ok(result);
    }

    /// <summary>Get a single document by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _documentService.GetByIdAsync(id, GetUserId(), GetUserRole(), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Delete a document by ID.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var deleted = await _documentService.DeleteAsync(id, GetUserId(), GetUserRole(), ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private Guid GetUserId() => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException("No user ID claim in token"));
    private string GetUserRole() =>
        User.FindFirstValue(ClaimTypes.Role)
        ?? User.FindFirstValue("role")
        ?? "Viewer";
}
