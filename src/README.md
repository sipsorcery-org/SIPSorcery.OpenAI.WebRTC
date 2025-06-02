# SIPSorcery.OpenAI.WebRTC

This repository contains a .NET library for interacting with [OpenAI's real-time WebRTC API](https://platform.openai.com/docs/guides/realtime-webrtc). It provides helper classes to negotiate peer connections, send and receive Opus audio frames and exchange control messages over a data channel.

## Features

- Establish a `RTCPeerConnection` with OpenAI using a REST based signalling helper.
- Send audio samples or pipe them from existing SIPSorcery media end points.
- Receive audio and transcript events via the data channel.
- `DataChannelMessenger` class to assist with sending session updates, function call results and response prompts.
- Designed to work with dependency injection or standalone instances.

The solution files are located under `src/` and `examples/`.

## Installation

Install the library from NuGet:

```bash
dotnet add package SIPSorcery.OpenAI.WebRTC
```

## Usage

### Console/WinForms Direct WebRTC Connection to OpenAI Realtime End Point

See GetStarted example for full source.

```csharp
using SIPSorcery.OpenAIWebRTC;
using SIPSorcery.OpenAIWebRTC.Models;

// Create the new WebRTC end point provided by this library (nothing starts yet).
var webrtcEndPoint = new WebRTCEndPoint(openAiKey, logger);

// Initialise default Windows audio devices and wire up event handlers to the WebRTC end point.
var windowsAudioEp = InitialiseWindowsAudioEndPoint();
webrtcEndPoint.ConnectAudioEndPoint(windowsAudioEp);

// Tell the WebRTC end point to start the connection attempt to the OpenAI Realtime WebRTC end point.
var negotiateConnectResult = await webrtcEndPoint.StartConnect();

// Wait for the connection to establish and then optionally update the session, start a conversation 
// and process data channel messages.

webrtcEndPoint.OnPeerConnectionConnected += () =>
{
    Log.Logger.Information("WebRTC peer connection established.");

    var voice = RealtimeVoicesEnum.verse;

    // Optionally send a session update message to adjust the session parameters.
    var sessionUpdateResult = webrtcEndPoint.DataChannelMessenger.SendSessionUpdate(
        voice,
        "Keep it short.",
        transcriptionModel: TranscriptionModelEnum.Whisper1);

    if (sessionUpdateResult.IsLeft)
    {
        Log.Logger.Error($"Failed to send rsession update message: {sessionUpdateResult.LeftAsEnumerable().First()}");
    }

    // Trigger the conversation by sending a response create message.
    var result = webrtcEndPoint.DataChannelMessenger.SendResponseCreate(voice, "Say Hi!");
    if (result.IsLeft)
    {
        Log.Logger.Error($"Failed to send response create message: {result.LeftAsEnumerable().First()}");
    }
};

webrtcEndPoint.OnDataChannelMessage += (dc, message) =>
{
    var log = message switch
    {
        RealtimeServerEventSessionUpdated sessionUpdated => $"Session updated: {sessionUpdated.ToJson()}",
        RealtimeServerEventConversationItemInputAudioTranscriptionDelta inputDelta => $"ME ⌛: {inputDelta.Delta?.Trim()}",
        RealtimeServerEventConversationItemInputAudioTranscriptionCompleted inputTranscript => $"ME ✅: {inputTranscript.Transcript?.Trim()}",
        RealtimeServerEventResponseAudioTranscriptDelta responseDelta => $"AI ⌛: {responseDelta.Delta?.Trim()}",
        RealtimeServerEventResponseAudioTranscriptDone responseTranscript => $"AI ✅: {responseTranscript.Transcript?.Trim()}",
        _ => $"Received {message.Type} -> {message.GetType().Name}"
    };

    if (log != string.Empty)
    {
        Log.Information(log);
    }
};

```

### ASP.NET WebRTC Bridge: Browser <- ASP.NET Bridge -> OpenAI Realtime End Point

See BrowserBridge example for full source.

```csharp
using SIPSorcery.OpenAIWebRTC;
using SIPSorcery.OpenAIWebRTC.Models;

// Set up an ASP.NET web socket to listen for connections.
// The web socket is NOT used for the connection to OpenAI. It's a covenience signalling channel to allow the browser
// to establish a WebRTC connection with the ASP.NET app.

app.Map("/ws", async (HttpContext context,
    [FromServices] IWebRTCEndPoint openAiWebRTCEndPoint) =>
{
    Log.Debug("Web socket client connection established.");

    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        // Create the ASP.NET WebRTC peer to connect to the browser.

        var webSocketPeer = new WebRTCWebSocketPeerAspNet(
            webSocket,
            CreateBrowserPeerConnection,
            null,
            RTCSdpType.offer);

        // Start the WebRTC connection attempt to the browser.

        var browserPeerTask = webSocketPeer.Run();

        // Start the attempt to connect to the OpenAI WebRTC end point in parallel with the 
        // browser connection which is already underway.

        SetOpenAIPeerEventHandlers(openAiWebRTCEndPoint);
        var openAiPeerTask = openAiWebRTCEndPoint.StartConnect(config);

        // Wait for both WebRTC connections to establish.

        await Task.WhenAll(browserPeerTask, openAiPeerTask);

        // Wire up the event handlers to connect the browser's audio to the openAIWebRTCEndPoint instance. 
        // This is the equivalent of connecting the audio to local Windows audio devices but in this case
        // the Browser audio stream is being wired up to the OpenAI audio stream.
        // It's much simpler to connect the browser directly to OpenAI, and this library is not needed for that.
        // The advantage of having an ASP.NET app in the middle is for things like capturing the audio transcription
        // or using local functions that can be handled by the ASP.NET app.

        ConnectPeers(webSocketPeer.RTCPeerConnection, openAiWebRTCEndPoint);

        Log.Debug("Web socket closing with WebRTC peer connection in state {state}.", webSocketPeer.RTCPeerConnection?.connectionState);
    }
    else
    {
        // Not a WebSocket request
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});
```

## Examples

Several sample applications demonstrating different scenarios are available in the `examples` folder:

- **GetStarted** – minimal console program that connects your microphone to OpenAI.
- **AliceAndBob** – runs two OpenAI peers and routes their audio between each other with a waveform display.
- **LocalFunctions** – showcases the local function calling feature.
- **GetPaid** – extends local functions to simulate payment requests.
- **DependencyInjection** – illustrates using the library with .NET DI.
- **BrowserBridge** – ASP.NET Core application bridging a browser WebRTC client to OpenAI.

Each example folder contains its own README with usage instructions.

## License

Distributed under the BSD 3‑Clause license with an additional BDS BY‑NC‑SA restriction. See [LICENSE.md](LICENSE.md) for details.
