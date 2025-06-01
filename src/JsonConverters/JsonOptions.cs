﻿//-----------------------------------------------------------------------------
// Filename: JsonOptions.cs
//
// Description: Convenience method for setting System.Text JSON serialisation options.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 09 Jun 2024  Aaron Clauson   Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License and the additional
// BDS BY-NC-SA restriction, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Text.Json.Serialization;
using System.Text.Json;

namespace SIPSorcery.OpenAI.WebRTC;

public class JsonOptions
{
    public static readonly JsonSerializerOptions Default;

    static JsonOptions()
    {
        Default = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = true,
            Converters =
                {
                    // Allow enum values or member attribute values,e.g. [EnumMember(Value = "xxx")] to be deserialised from strings.
                    new JsonStringEnumMemberConverter(),

                    // Allows "true" and "false" strings to be deserialised to booleans.
                    new BooleanAsStringConverter(),

                    // Newtonsoft allows numeric values to be deserialised from strings.
                    // This is not the default behaviour in System.Text.Json so use a custom converter.
                    //new NumericConverter<int>(),
                    //new NumericConverter<long>(),
                    //new NumericConverter<float>(),
                    //new NumericConverter<double>(),
                    //new NumericConverter<decimal>()
                },
            PropertyNamingPolicy = null // PacalCase by default.
        };
    }
}
