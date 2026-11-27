namespace DocumentManagement.Api.DTOs.Documents;
public class DocumentResponseDto
{
    public Guid Id { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; }
}