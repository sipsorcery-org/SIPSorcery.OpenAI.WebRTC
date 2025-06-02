//-----------------------------------------------------------------------------
// Filename: IsAliveUnitTest.cs
//
// Description: Minimal test to check the test framework.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 02 Jun 2025  Aaron Clauson   Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
// BDS BY-NC-SA restriction, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Xunit;

namespace SIPSorcery.OpenAIWebRTC.UnitTests;

[Trait("Category", "unit")]
public class IsAliveUnitTest
{
    private ILogger logger = null;

    public IsAliveUnitTest(Xunit.Abstractions.ITestOutputHelper output)
    {
        logger = TestLogHelper.InitTestLogger(output);
    }

    [Fact]
    public void Is_Alive_Test()
    {
        logger.LogDebug("--> {MethodName}", System.Reflection.MethodBase.GetCurrentMethod().Name);
        logger.BeginScope(System.Reflection.MethodBase.GetCurrentMethod().Name);
    }
}
