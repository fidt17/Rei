namespace ReiEditor.Mcp.Contracts;

public sealed class ReiMcpOperationException : Exception
{
    public string Code { get; }

    public ReiMcpOperationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
