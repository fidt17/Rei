using System;
using System.Runtime.InteropServices;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.EditorConsole;

namespace ReiEditor.Models.Services.Logging.Engine;

public class ClientLogger : IClientLogger
{
	private readonly IClientApi.CallbackDelegate _logCallbackDelegate;
	
	private readonly IEditorConsoleService _editorConsoleService;
	private readonly IClientApi _clientApi;

	public ClientLogger(IEditorConsoleService editorConsoleService, IClientApi clientApi)
	{
		_editorConsoleService = editorConsoleService;
		_clientApi = clientApi;
		_logCallbackDelegate = HandleClientLogEvent;
	}

	public void SubscribeToClient()
	{
		_clientApi.AddLogCallback(Marshal.GetFunctionPointerForDelegate(_logCallbackDelegate));
	}

	private void HandleClientLogEvent(IntPtr messagePtr)
	{
		var messageStruct = Marshal.PtrToStructure<EngineLogMessage>(messagePtr);
		var logMessage = new LogMessage(LogScopeEnum.Engine, messageStruct.Level, DateTime.Now, messageStruct.Message, $"{messageStruct.Scope}\n{messageStruct.Details}");
		_editorConsoleService.Log(logMessage);
	}
}