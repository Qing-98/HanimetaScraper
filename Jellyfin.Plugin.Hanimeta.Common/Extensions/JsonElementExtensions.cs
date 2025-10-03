using System;
using System.Globalization;
using System.Text.Json;

namespace Jellyfin.Plugin.Hanimeta.Common.Extensions
{
    /// <summary>
    /// Convenience extension methods for safe JsonElement property access.
    /// </summary>
    public static class JsonElementExtensions
    {
        /// <summary>
        /// Safely gets a string value for the specified property name or returns null if the property is missing or of an unsupported kind.
        /// </summary>
        /// <param name="element">The JSON element to read from.</param>
        /// <param name="propertyName">The name of the property to read.</param>
        /// <returns>The string value or null.</returns>
        public static string? GetStringOrNull(this JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
            {
                return null;
            }

            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
        }

        /// <summary>
        /// Safely gets an integer value for the specified property name or returns null if it cannot be parsed.
        /// </summary>
        /// <param name="element">The JSON element to read from.</param>
        /// <param name="propertyName">The name of the property to read.</param>
        /// <returns>The integer value or null.</returns>
        public static int? GetIntOrNull(this JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
            {
                return null;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var v))
            {
                return v;
            }

            if (prop.ValueKind == JsonValueKind.String &&
                int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
            {
                return s;
            }

            return null;
        }

        /// <summary>
        /// Safely gets a floating point value for the specified property name or returns null if it cannot be parsed.
        /// </summary>
        /// <param name="element">The JSON element to read from.</param>
        /// <param name="propertyName">The name of the property to read.</param>
        /// <returns>The float value or null.</returns>
        public static float? GetFloatOrNull(this JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
            {
                return null;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d))
            {
                return (float)d;
            }

            if (prop.ValueKind == JsonValueKind.String &&
                float.TryParse(prop.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var f))
            {
                return f;
            }

            return null;
        }

        /// <summary>
        /// Safely gets a DateTimeOffset value for the specified property name or returns null if it cannot be parsed.
        /// Supports ISO date strings and Unix epoch seconds.
        /// </summary>
        /// <param name="element">The JSON element to read from.</param>
        /// <param name="propertyName">The name of the property to read.</param>
        /// <returns>The DateTimeOffset value or null.</returns>
        public static DateTimeOffset? GetDateTimeOffsetOrNull(this JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
            {
                return null;
            }

            if (prop.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(
                    prop.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dto))
            {
                return dto;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var unix))
            {
                try
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unix);
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
