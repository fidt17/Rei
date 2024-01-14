using System;
using System.Runtime.InteropServices;
using ReiEditor.Models.EditorApp.Console;
using ReiEditor.Models.Services.Engine.Api;

namespace ReiEditor.Models.Services.Logging.Engine;

public class EngineLogger : IEngineLogger
{
	private readonly IEngineApi.CallbackDelegate _logCallbackDelegate;
	
	private readonly IEditorConsoleService _editorConsoleService;
	private readonly IEngineApi _engineApi;

	public EngineLogger(IEditorConsoleService editorConsoleService, IEngineApi engineApi)
	{
		_editorConsoleService = editorConsoleService;
		_engineApi = engineApi;
		_logCallbackDelegate = HandleClientLogEvent;
	}

	public void SubscribeToClient()
	{
		_engineApi.AddLogCallback(Marshal.GetFunctionPointerForDelegate(_logCallbackDelegate));
	}

	private void HandleClientLogEvent(IntPtr messagePtr)
	{
		var messageStruct = Marshal.PtrToStructure<EngineLogMessage>(messagePtr);
		var logMessage = new LogMessage(LogScopeEnum.Engine, messageStruct.Level, DateTime.Now, messageStruct.Message, $"{messageStruct.Scope}\n{messageStruct.Details}");
		_editorConsoleService.Log(logMessage);
	}
}