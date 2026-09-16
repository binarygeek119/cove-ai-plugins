namespace Cove.Ai.Abstractions;

/// <summary>
/// Thrown when an image model refuses a prompt (safety/content policy), so callers can strip
/// details and retry. Other HTTP/API failures stay as <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class AiImageRejectedException : InvalidOperationException
{
    public AiImageRejectedException(string message) : base(message)
    {
    }
}
