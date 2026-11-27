using DocumentManagement.Api.Data;
using DocumentManagement.Api.DTOs.Documents;
using DocumentManagement.Api.Models;
using DocumentManagement.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Api.Controllers;

[Route("api/documents")]
[ApiController]
public class DocumentsController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    // Allowed file types. The content type saved in the database comes from this list,
    // not from the client, because the client-supplied value can be faked.
    private static readonly Dictionary<string, string> AllowedTypes = new()
    {
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _storage;

    public DocumentsController(
        ApplicationDbContext context, 
        IFileStorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    // POST /api/documents
    [HttpPost]
    public async Task<ActionResult<DocumentResponseDto>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Problem( title: "Invalid file", 
                            detail: "The uploaded file is empty.", 
                            statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return Problem( title: "File too large", 
                            detail: "The maximum allowed file size is 10 MB.", 
                            statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        // Path.GetFileName strips any folder part the client may have sent (e.g. "..\..\x.pdf").
        var originalFileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 255)
        {
            return Problem( title: "Invalid file name", 
                            detail: "The file name is missing or longer than 255 characters.", 
                            statusCode: StatusCodes.Status400BadRequest);
        }

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

        if (!AllowedTypes.TryGetValue(extension, out var contentType))
        {
            return Problem( title: "Unsupported file type",
                            detail: $"Allowed file types: {string.Join(", ", AllowedTypes.Keys)}",
                            statusCode: StatusCodes.Status415UnsupportedMediaType);
        }

        await using var stream = file.OpenReadStream();
        var storedFileName = await _storage.SaveAsync(stream, extension, cancellationToken);

        var document = new Document
        {
            Id                  = Guid.NewGuid(),
            OriginalFileName    = originalFileName,
            StoredFileName      = storedFileName,
            ContentType         = contentType,
            FileSize            = file.Length,
            UploadedAt          = DateTime.UtcNow
        };

        _context.Documents.Add(document);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The file is already on disk but the database row failed, so remove the file
            // to avoid leaving an orphan behind.
            _storage.Delete(storedFileName);
            throw;
        }

        var response = new DocumentResponseDto
        {
            Id                  = document.Id,
            OriginalFileName    = document.OriginalFileName,
            ContentType         = document.ContentType,
            FileSize            = document.FileSize,
            UploadedAt          = document.UploadedAt
        };

        return Created($"/api/documents/{document.Id}", response);
    }

    // GET /api/documents
    [HttpGet]
    public async Task<ActionResult<List<DocumentResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var documents = await _context.Documents
            .AsNoTracking()
            .OrderByDescending(document => document.UploadedAt)
            .Select(document => new DocumentResponseDto
            {
                Id                  = document.Id,
                OriginalFileName    = document.OriginalFileName,
                ContentType         = document.ContentType,
                FileSize            = document.FileSize,
                UploadedAt          = document.UploadedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(documents);
    }

    // GET /api/documents/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

        if (document == null)
        {
            return Problem(title: "Document not found",
                           detail: $"No document exists with id {id}.",
                           statusCode: StatusCodes.Status404NotFound);
        }

        var response = new DocumentResponseDto
        {
            Id = document.Id,
            OriginalFileName = document.OriginalFileName,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            UploadedAt = document.UploadedAt
        };

        return Ok(response);
    }

    // GET /api/documents/{id}/download
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

        if (document == null)
        {
            return Problem( title: "Document not found",
                            detail: $"No document exists with id {id}.",
                            statusCode: StatusCodes.Status404NotFound);
        }

        var stream = _storage.OpenRead(document.StoredFileName);

        if (stream == null)
        {
            // The database record exists but the physical file is gone.
            return Problem( title: "File not found",
                            detail: "The document record exists but its stored file could not be found.",
                            statusCode: StatusCodes.Status404NotFound);
        }

        // File() closes the stream after sending the response.
        return File(stream, document.ContentType, document.OriginalFileName);
    }

    // DELETE /api/documents/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

        if (document == null)
        {
            return Problem( title: "Document not found",
                            detail: $"No document exists with id {id}.",
                            statusCode: StatusCodes.Status404NotFound);
        }

        // Delete the file first. If this fails, the database record is kept,
        // so the record never points to a file we silently lost track of.
        try
        {
            _storage.Delete(document.StoredFileName);
        }
        catch (IOException)
        {
            return Problem( title: "Delete failed",
                            detail: "The stored file could not be deleted. Please try again.",
                            statusCode: StatusCodes.Status500InternalServerError);
        }

        // If the file was already missing, Delete returns false and we still remove the record.
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}