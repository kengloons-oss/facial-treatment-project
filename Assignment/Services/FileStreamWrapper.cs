public class FileStreamWrapper : IFormFile
{
    private readonly Stream _fileStream;
    private readonly string _fileName;
    private bool _disposed = false;

    public FileStreamWrapper(string filePath)
    {
        _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        _fileName = Path.GetFileName(filePath);
        ContentType = "application/octet-stream"; // You can change this based on your file type
        Length = _fileStream.Length;
    }

    public string ContentType { get; set; }

    // File name retrieved from the file path
    public string FileName => _fileName;

    public long Length { get; }

    // The name of the form element (in case you want to access the form input's name)
    public string Name => "file";

    // The content disposition (usually set to "attachment" or "inline")
    public string ContentDisposition => $"attachment; filename={FileName}";

    // Headers for the file (optional, but included for completeness)
    public IHeaderDictionary Headers => new HeaderDictionary();

    // Open the file as a stream
    public Stream OpenReadStream()
    {
        return _fileStream;
    }

    public void CopyTo(Stream target)
    {
        _fileStream.CopyTo(target);
    }

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
    {
        return _fileStream.CopyToAsync(target, cancellationToken);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _fileStream?.Dispose();
            _disposed = true;
        }
    }
}
