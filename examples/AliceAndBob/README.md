# AliceAndBob Example

Runs two OpenAI WebRTC sessions ("Alice" and "Bob") and pipes their audio
between each other. A simple OpenGL scope visualises which side is speaking.

## Usage

```bash
export OPENAI_API_KEY="<your OpenAI key>"
dotnet run
```

You'll need a Windows machine with audio devices and .NET 8.0 installed.
