//-----------------------------------------------------------------------------
// Filename: IWebRTCEndPoint.cs
//
// Description: Interface for the OpenAI WebRTC peer connection.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 18 May 2025  Aaron Clauson   Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License and the additional
// BDS BY-NC-SA restriction, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace SIPSorcery.OpenAI.WebRTC;

public interface IWebRTCEndPoint
{
    RTCPeerConnection? PeerConnection { get; }

    public DataChannelMessenger DataChannelMessenger { get; }

    event Action? OnPeerConnectionConnected;

    event Action? OnPeerConnectionFailed;

    event Action? OnPeerConnectionClosed;

    event Action<EncodedAudioFrame>? OnAudioFrameReceived;

    event Action<RTCDataChannel, OpenAIServerEventBase>? OnDataChannelMessage;

    Task<Either<Error, Unit>> StartConnect(RTCConfiguration? pcConfig = null, string? model = null);

    void SendAudio(uint durationMilliseconds, byte[] encodedAudio);

    void SendDataChannelMessage(OpenAIServerEventBase message);

    void Close();
}
