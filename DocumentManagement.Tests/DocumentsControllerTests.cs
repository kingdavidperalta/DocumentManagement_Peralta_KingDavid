using DocumentManagement.Api.Controllers;
using DocumentManagement.Api.Data;
using DocumentManagement.Api.DTOs.Documents;
using DocumentManagement.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.Tests;

public class DocumentsControllerTests
{
    private readonly ApplicationDbContext _context;
    private readonly FakeFileStorageService _storage;
    private readonly DocumentsController _controller;

    // xUnit creates a new instance of this class for every test,
    // so each test starts with an empty database and empty storage.
    public DocumentsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _storage = new FakeFileStorageService();
        _controller = new DocumentsController(_context, _storage);
    }

    // Builds a fake uploaded file of the given size.
    private static IFormFile CreateFormFile(string fileName, int sizeInBytes)
    {
        var stream = new MemoryStream(new byte[sizeInBytes]);
        return new FormFile(stream, 0, sizeInBytes, "file", fileName);
    }

    // Adds a document to the database and its file to the fake storage.
    private async Task<Document> SeedDocumentAsync()
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            OriginalFileName = "report.pdf",
            StoredFileName = "stored-report.pdf",
            ContentType = "application/pdf",
            FileSize = 3,
            UploadedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _storage.Files[document.StoredFileName] = new byte[] { 1, 2, 3 };

        return document;
    }

    // ---------- Required tests ----------

    [Fact]
    public async Task Upload_ValidPdf_ReturnsCreatedAndSavesFileAndRecord()
    {
        var file = CreateFormFile("report.pdf", 1024);

        var result = await _controller.Upload(file, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var dto = Assert.IsType<DocumentResponseDto>(created.Value);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("report.pdf", dto.OriginalFileName);
        Assert.Equal("application/pdf", dto.ContentType);
        Assert.Equal(1024, dto.FileSize);

        Assert.Equal(1, _context.Documents.Count());
        Assert.Single(_storage.Files);
    }

    [Fact]
    public async Task GetById_ExistingDocument_ReturnsOkWithMetadata()
    {
        var document = await SeedDocumentAsync();

        var result = await _controller.GetById(document.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<DocumentResponseDto>(ok.Value);

        Assert.Equal(document.Id, dto.Id);
        Assert.Equal("report.pdf", dto.OriginalFileName);
    }

    [Fact]
    public async Task GetById_NonExistentDocument_ReturnsNotFound()
    {
        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task Upload_FileLargerThan10MB_ReturnsPayloadTooLarge()
    {
        var tenMegabytes = 10 * 1024 * 1024;
        var file = CreateFormFile("big.pdf", tenMegabytes + 1);

        var result = await _controller.Upload(file, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, problem.StatusCode);

        // Nothing should have been saved.
        Assert.Empty(_context.Documents);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Delete_ExistingDocument_ReturnsNoContentAndRemovesFileAndRecord()
    {
        var document = await SeedDocumentAsync();

        var result = await _controller.Delete(document.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(_context.Documents);
        Assert.Empty(_storage.Files);
    }

    // ---------- Extra tests ----------

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        var file = CreateFormFile("empty.pdf", 0);

        var result = await _controller.Upload(file, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_ReturnsUnsupportedMediaType()
    {
        var file = CreateFormFile("virus.exe", 100);

        var result = await _controller.Upload(file, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, problem.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentDocument_ReturnsNotFound()
    {
        var result = await _controller.Delete(Guid.NewGuid(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenFileDeleteFails_ReturnsServerErrorAndKeepsRecord()
    {
        var document = await SeedDocumentAsync();
        _storage.ThrowOnDelete = true;

        var result = await _controller.Delete(document.Id, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.StatusCode);

        // The database record must still exist.
        Assert.Equal(1, _context.Documents.Count());
    }
}