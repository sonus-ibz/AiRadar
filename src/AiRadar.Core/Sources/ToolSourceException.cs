namespace AiRadar.Core.Sources;

/// <summary>A source could not be read; the message is safe to show to the user.</summary>
public sealed class ToolSourceException(string message, Exception? innerException = null)
    : Exception(message, innerException);
