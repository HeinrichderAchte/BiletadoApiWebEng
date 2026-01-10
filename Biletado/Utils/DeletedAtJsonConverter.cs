using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Biletado.DTOs.Request;

namespace Biletado.Utils
{
    public class DeletedAtJsonConverter : JsonConverter<DeletedAtField>
    {
        public override DeletedAtField Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var result = new DeletedAtField { Present = true, Value = null };

            if (reader.TokenType == JsonTokenType.Null)
            {
                // explicit null
                return result;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return result;

                // Try to parse flexible ISO formats
                if (DateTimeOffset.TryParseExact(s,
                    new[] {
                        "yyyy-MM-dd'T'HH:mm:ss'Z'",
                        "yyyy-MM-dd'T'HH:mm:ss.F'Z'",
                        "yyyy-MM-dd'T'HH:mm:ss.FF'Z'",
                        "yyyy-MM-dd'T'HH:mm:ss.FFF'Z'",
                        "yyyy-MM-dd'T'HH:mm:ss.FFFF'Z'",
                        "yyyy-MM-dd'T'HH:mm:ss.FFFFF'Z'",
                        "yyyy-MM-dd'T'HH:mm:ss.FFFFFF'Z'",
                        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
                        "o"
                    },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dto))
                {
                    result.Value = dto.UtcDateTime;
                }
                else
                {
                    if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto2))
                    {
                        result.Value = dto2.UtcDateTime;
                    }
                }

                return result;
            }

            // For numbers/objects/etc treat as present but null
            return result;
        }

        public override void Write(Utf8JsonWriter writer, DeletedAtField value, JsonSerializerOptions options)
        {
            if (!value.Present)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.Value.HasValue)
            {
                writer.WriteStringValue(value.Value.Value.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}

