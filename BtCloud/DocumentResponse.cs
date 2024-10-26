using System.Text.Json.Serialization;

namespace BtCloudDownload.BtCloud;

public record DocumentResponse(DocumentFilesHolder FilesHolder);

public record DocumentFilesHolder(DocumentFiles Files, DocumentCursor? Cursor);

public record DocumentFiles(DocumentFile[] File);

public class DocumentCursor 
{
    [JsonPropertyName("$")]
    public string Cursor { get; init; } = null!;
}

public record DocumentFile(DocumentLink[] Link, DocumentName Name,
DocumentParentPath ParentPath, DocumentRepository Repository);

public class DocumentLink 
{
    [JsonPropertyName("$")]
    public string Path { get; init; } = null!;

    [JsonPropertyName("@rel")]
    public string LinkType { get; init; } = null!;
}

public class DocumentName
{
    [JsonPropertyName("$")]
    public string Name { get; init; } = null!;
}

public class DocumentParentPath
{
    [JsonPropertyName("$")]
    public string ParentPath { get; init; } = null!;
}

public class DocumentRepository
{
    [JsonPropertyName("$")]
    public string Repository { get; init; } = null!;
}