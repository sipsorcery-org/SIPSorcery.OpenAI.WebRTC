# SIPSorcery.OpenAI.WebRTC

This repository contains a .NET library for interacting with [OpenAI's real-time WebRTC API](https://platform.openai.com/docs/guides/realtime-webrtc). It provides helper classes to negotiate peer connections, send and receive Opus audio frames and exchange control messages over a data channel.

## Features

- Establish a `RTCPeerConnection` with OpenAI using a REST based signalling helper.
- Send audio samples or pipe them from existing SIPSorcery media end points.
- Receive audio and transcript events via the data channel.
- `DataChannelMessenger` class to assist with sending session updates, function call results and response prompts.
- Designed to work with dependency injection or standalone instances.

## Building

```bash
# Build the library and all example projects
dotnet build
```

The solution files are located under `src/` and `examples/`.

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
