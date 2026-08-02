namespace CsoKit.Core.Formats.Containers;

public sealed class BlockContainerReadException(
    string code,
    string message,
    int? blockIndex = null,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public BlockContainerReadException()
        : this("BlockContainerReadFailed", "Block container read failed.")
    {
    }

    public BlockContainerReadException(string message)
        : this("BlockContainerReadFailed", message)
    {
    }

    public BlockContainerReadException(string message, Exception innerException)
        : this("BlockContainerReadFailed", message, blockIndex: null, innerException)
    {
    }

    public string Code { get; } = string.IsNullOrWhiteSpace(code)
        ? "BlockContainerReadFailed"
        : code;

    public int? BlockIndex { get; } = blockIndex;
}