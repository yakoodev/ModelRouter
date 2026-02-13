namespace MultiLlm.Extras.ImageProcessing;

public interface IImagePreprocessor
{
    Task<byte[]> ResizeIfNeededAsync(byte[] image, string mimeType, CancellationToken cancellationToken = default);
}
