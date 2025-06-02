//-----------------------------------------------------------------------------
// Filename: IWebRTCRestClient.cs
//
// Description: Interface for the OpenAI WebRTC REST client.
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

using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

namespace SIPSorcery.OpenAIWebRTC;

public interface IWebRTCRestClient
{
    Task<Either<Error, string>> CreateEphemeralKeyAsync(
        string model = WebRTCRestClient.OPENAI_REALTIME_DEFAULT_MODEL,
        OpenAIVoicesEnum voice = OpenAIVoicesEnum.shimmer,
        CancellationToken ct = default);

    Task<Either<Error, string>> GetSdpAnswerAsync(
        string offerSdp,
        string model = WebRTCRestClient.OPENAI_REALTIME_DEFAULT_MODEL,
        CancellationToken ct = default);
}
