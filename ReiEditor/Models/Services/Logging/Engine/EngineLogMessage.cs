using System.Runtime.InteropServices;

namespace ReiEditor.Models.Services.Logging.Engine;

[StructLayout(LayoutKind.Sequential)]
public struct EngineLogMessage
{
	public string Scope;
	public LogLevelEnum Level;
	public string Message;
	public string Details;
}