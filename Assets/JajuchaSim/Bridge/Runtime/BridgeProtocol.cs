using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JajuchaSim.Bridge
{
    /// <summary>
    /// Serializes/deserializes <see cref="BridgeMessage"/> to/from newline-delimited
    /// JSON format.
    ///
    /// This uses a lightweight manual JSON handler to avoid external dependencies.
    /// The protocol format is simple enough that a full JSON library is unnecessary.
    /// </summary>
    public static class BridgeProtocol
    {
        // --- Serialize ---

        /// <summary>
        /// Serializes a <see cref="BridgeMessage"/> to a JSON string (without trailing newline).
        /// </summary>
        public static string Serialize(BridgeMessage msg)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));

            var sb = new StringBuilder();
            sb.Append('{');
            AppendString(sb, "type", msg.Type);
            sb.Append(',');

            if (msg.Type == "hello")
            {
                AppendInt(sb, "protocol", msg.Protocol);
                sb.Append(',');
                AppendString(sb, "client", msg.Client);
            }
            else if (msg.Type == "hello_ack")
            {
                AppendInt(sb, "protocol", msg.Protocol);
                sb.Append(',');
                AppendString(sb, "simulator", msg.Simulator);
            }
            else if (msg.Type == "command")
            {
                AppendInt(sb, "id", msg.Id);
                sb.Append(',');
                AppendString(sb, "name", msg.Name);
                if (msg.Payload != null && msg.Payload.Count > 0)
                {
                    sb.Append(',');
                    AppendPayload(sb, msg.Payload);
                }
            }
            else if (msg.Type == "response")
            {
                AppendInt(sb, "id", msg.Id);
                sb.Append(',');
                AppendBool(sb, "ok", msg.Ok);

                // Image response fields (for binary payload responses)
                if (!string.IsNullOrEmpty(msg.PayloadType))
                {
                    sb.Append(',');
                    AppendString(sb, "payload_type", msg.PayloadType);
                }
                if (msg.ImageWidth > 0)
                {
                    sb.Append(',');
                    AppendInt(sb, "width", msg.ImageWidth);
                }
                if (msg.ImageHeight > 0)
                {
                    sb.Append(',');
                    AppendInt(sb, "height", msg.ImageHeight);
                }
                if (!string.IsNullOrEmpty(msg.ImageFormat))
                {
                    sb.Append(',');
                    AppendString(sb, "format", msg.ImageFormat);
                }
                if (msg.ImageLength > 0)
                {
                    sb.Append(',');
                    AppendInt(sb, "length", msg.ImageLength);
                }

                if (msg.LidarRayCount > 0)
                {
                    sb.Append(','); AppendLong(sb, "frame_id", msg.LidarFrameId);
                    sb.Append(','); AppendLong(sb, "scan_tick", msg.LidarSimulationTick);
                    sb.Append(','); AppendDouble(sb, "scan_time", msg.LidarSimulationTime);
                    sb.Append(','); AppendInt(sb, "ray_count", msg.LidarRayCount);
                    sb.Append(','); AppendFloat(sb, "angle_min_deg", msg.LidarAngleMinDeg);
                    sb.Append(','); AppendFloat(sb, "angle_max_deg", msg.LidarAngleMaxDeg);
                    sb.Append(','); AppendFloat(sb, "angle_increment_deg", msg.LidarAngleIncrementDeg);
                    sb.Append(','); AppendFloat(sb, "max_distance_cm", msg.LidarMaxDistanceCm);
                }

                if (msg.Payload != null && msg.Payload.Count > 0)
                {
                    sb.Append(',');
                    AppendPayload(sb, msg.Payload);
                }
                if (msg.Error != null)
                {
                    sb.Append(',');
                    AppendError(sb, msg.Error);
                }
            }
            else if (msg.Type == "error")
            {
                AppendString(sb, "code", msg.Error?.Code ?? "UNKNOWN");
                if (!string.IsNullOrEmpty(msg.Error?.Message))
                {
                    sb.Append(',');
                    AppendString(sb, "message", msg.Error.Message);
                }
            }

            sb.Append('}');
            return sb.ToString();
        }

        // --- Deserialize ---

        /// <summary>
        /// Deserializes a JSON string to a <see cref="BridgeMessage"/>.
        /// Returns null if the JSON is invalid.
        /// </summary>
        public static BridgeMessage Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var dict = ParseSimpleJson(json);
            if (dict == null || dict.Count == 0) return null;

            if (!dict.TryGetValue("type", out var typeObj)) return null;
            string type = typeObj as string;
            if (string.IsNullOrEmpty(type)) return null;

            var msg = new BridgeMessage { Type = type };

            if (type == "hello")
            {
                if (dict.TryGetValue("protocol", out var proto))
                    msg.Protocol = Convert.ToInt32(proto);
                if (dict.TryGetValue("client", out var client))
                    msg.Client = client as string ?? "";
            }
            else if (type == "command")
            {
                if (dict.TryGetValue("id", out var id))
                    msg.Id = Convert.ToInt32(id);
                if (dict.TryGetValue("name", out var name))
                    msg.Name = name as string ?? "";
                if (dict.TryGetValue("payload", out var payloadObj))
                {
                    msg.Payload = payloadObj as Dictionary<string, object>;
                }
            }
            else if (type == "response")
            {
                if (dict.TryGetValue("id", out var id))
                    msg.Id = Convert.ToInt32(id);
                if (dict.TryGetValue("ok", out var ok))
                    msg.Ok = Convert.ToBoolean(ok);

                // Image response fields
                if (dict.TryGetValue("payload_type", out var pt))
                    msg.PayloadType = pt as string ?? "";
                if (dict.TryGetValue("width", out var w))
                    msg.ImageWidth = Convert.ToInt32(w);
                if (dict.TryGetValue("height", out var h))
                    msg.ImageHeight = Convert.ToInt32(h);
                if (dict.TryGetValue("format", out var fmt))
                    msg.ImageFormat = fmt as string ?? "";
                if (dict.TryGetValue("length", out var len))
                    msg.ImageLength = Convert.ToInt32(len);
                if (dict.TryGetValue("frame_id", out var frameId))
                    msg.LidarFrameId = Convert.ToInt64(frameId);
                if (dict.TryGetValue("scan_tick", out var scanTick))
                    msg.LidarSimulationTick = Convert.ToInt64(scanTick);
                if (dict.TryGetValue("scan_time", out var scanTime))
                    msg.LidarSimulationTime = Convert.ToDouble(scanTime);
                if (dict.TryGetValue("ray_count", out var rayCount))
                    msg.LidarRayCount = Convert.ToInt32(rayCount);
                if (dict.TryGetValue("angle_min_deg", out var angleMin))
                    msg.LidarAngleMinDeg = Convert.ToSingle(angleMin);
                if (dict.TryGetValue("angle_max_deg", out var angleMax))
                    msg.LidarAngleMaxDeg = Convert.ToSingle(angleMax);
                if (dict.TryGetValue("angle_increment_deg", out var angleIncrement))
                    msg.LidarAngleIncrementDeg = Convert.ToSingle(angleIncrement);
                if (dict.TryGetValue("max_distance_cm", out var maxDistance))
                    msg.LidarMaxDistanceCm = Convert.ToSingle(maxDistance);

                if (dict.TryGetValue("payload", out var payloadObj))
                {
                    msg.Payload = payloadObj as Dictionary<string, object>;
                }
                if (dict.TryGetValue("error", out var errObj) && errObj is Dictionary<string, object> errDict)
                {
                    msg.Error = new BridgeErrorDetail();
                    if (errDict.TryGetValue("code", out var code))
                        msg.Error.Code = code as string ?? "";
                    if (errDict.TryGetValue("message", out var message))
                        msg.Error.Message = message as string ?? "";
                }
            }

            return msg;
        }

        // --- Helper: append JSON strings ---

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append('"');
            sb.Append(':');
            sb.Append('"');
            if (value != null)
            {
                foreach (char c in value)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20)
                            {
                                sb.AppendFormat("\\u{0:X4}", (int)c);
                            }
                            else
                            {
                                sb.Append(c);
                            }
                            break;
                    }
                }
            }
            sb.Append('"');
        }

        private static void AppendInt(StringBuilder sb, string key, int value)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append('"');
            sb.Append(':');
            sb.Append(value);
        }

        private static void AppendLong(StringBuilder sb, string key, long value)
        {
            sb.Append('"'); sb.Append(key); sb.Append("\":"); sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendDouble(StringBuilder sb, string key, double value)
        {
            sb.Append('"'); sb.Append(key); sb.Append("\":"); sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendFloat(StringBuilder sb, string key, float value)
        {
            sb.Append('"'); sb.Append(key); sb.Append("\":"); sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendBool(StringBuilder sb, string key, bool value)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append('"');
            sb.Append(':');
            sb.Append(value ? "true" : "false");
        }

        private static void AppendPayload(StringBuilder sb, Dictionary<string, object> payload)
        {
            sb.Append('"');
            sb.Append("payload");
            sb.Append('"');
            sb.Append(':');
            sb.Append('{');
            bool first = true;
            foreach (var kvp in payload)
            {
                if (!first) sb.Append(',');
                first = false;
                AppendObject(sb, kvp.Key, kvp.Value);
            }
            sb.Append('}');
        }

        private static void AppendObject(StringBuilder sb, string key, object value)
        {
            if (value is int i)
            {
                AppendInt(sb, key, i);
            }
            else if (value is long l)
            {
                sb.Append('"'); sb.Append(key); sb.Append('"'); sb.Append(':');
                sb.Append(l);
            }
            else if (value is bool b)
            {
                AppendBool(sb, key, b);
            }
            else if (value is double d)
            {
                sb.Append('"'); sb.Append(key); sb.Append('"'); sb.Append(':');
                sb.Append(d.ToString("G"));
            }
            else if (value is float f)
            {
                sb.Append('"'); sb.Append(key); sb.Append('"'); sb.Append(':');
                sb.Append(f.ToString("G"));
            }
            else if (value is Dictionary<string, object> nestedDict)
            {
                sb.Append('"'); sb.Append(key); sb.Append('"'); sb.Append(':');
                sb.Append('{');
                bool first = true;
                foreach (var kvp in nestedDict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    AppendObject(sb, kvp.Key, kvp.Value);
                }
                sb.Append('}');
            }
            else
            {
                AppendString(sb, key, value?.ToString() ?? "");
            }
        }

        private static void AppendError(StringBuilder sb, BridgeErrorDetail error)
        {
            sb.Append('"');
            sb.Append("error");
            sb.Append('"');
            sb.Append(':');
            sb.Append('{');
            AppendString(sb, "code", error.Code);
            if (!string.IsNullOrEmpty(error.Message))
            {
                sb.Append(',');
                AppendString(sb, "message", error.Message);
            }
            sb.Append('}');
        }

        // --- Helper: parse simple JSON (subset for our protocol) ---

        /// <summary>
        /// Parses a simple JSON object (no arrays, only string/number/bool/null values
        /// and nested objects for "payload" and "error"). Returns a flat dictionary of
        /// top-level keys, with nested objects represented as Dictionary[string,object].
        /// </summary>
        internal static Dictionary<string, object> ParseSimpleJson(string json)
        {
            var result = new Dictionary<string, object>();
            int pos = 0;
            SkipWhitespace(json, ref pos);

            if (pos >= json.Length || json[pos] != '{') return null;
            pos++; // skip '{'

            while (pos < json.Length)
            {
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) return null;
                if (json[pos] == '}')
                {
                    pos++;
                    return result;
                }

                // Parse key
                string key = ParseString(json, ref pos);
                if (key == null) return null;

                SkipWhitespace(json, ref pos);
                if (pos >= json.Length || json[pos] != ':') return null;
                pos++; // skip ':'

                SkipWhitespace(json, ref pos);
                object value = ParseValue(json, ref pos);
                if (value == null && pos < json.Length && json[pos] != ',' && json[pos] != '}')
                {
                    // parsing failed
                    return null;
                }

                result[key] = value;

                SkipWhitespace(json, ref pos);
                if (pos >= json.Length) return null;
                if (json[pos] == '}')
                {
                    pos++;
                    return result;
                }
                if (json[pos] == ',')
                {
                    pos++;
                    continue;
                }
                return null; // unexpected character
            }

            return null;
        }

        private static void SkipWhitespace(string s, ref int pos)
        {
            while (pos < s.Length && char.IsWhiteSpace(s[pos]))
                pos++;
        }

        private static string ParseString(string s, ref int pos)
        {
            if (pos >= s.Length || s[pos] != '"') return null;
            pos++; // skip opening quote
            var sb = new StringBuilder();
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c == '"')
                {
                    pos++; // skip closing quote
                    return sb.ToString();
                }
                if (c == '\\')
                {
                    pos++;
                    if (pos >= s.Length) return null;
                    char esc = s[pos];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            // 4-digit hex
                            if (pos + 4 >= s.Length) return null;
                            string hex = s.Substring(pos + 1, 4);
                            sb.Append((char)Convert.ToInt32(hex, 16));
                            pos += 4;
                            break;
                        default: return null;
                    }
                    pos++;
                }
                else
                {
                    sb.Append(c);
                    pos++;
                }
            }
            return null; // unterminated string
        }

        private static object ParseValue(string s, ref int pos)
        {
            SkipWhitespace(s, ref pos);
            if (pos >= s.Length) return null;

            char c = s[pos];
            if (c == '"')
            {
                return ParseString(s, ref pos);
            }
            if (c == '{')
            {
                var result = ParseSimpleJson(s.Substring(pos), out int consumed);
                if (result != null)
                    pos += consumed;
                return result;
            }
            if (c == 't' && s.Substring(pos).StartsWith("true"))
            {
                pos += 4;
                return true;
            }
            if (c == 'f' && s.Substring(pos).StartsWith("false"))
            {
                pos += 5;
                return false;
            }
            if (c == 'n' && s.Substring(pos).StartsWith("null"))
            {
                pos += 4;
                return null;
            }
            if (c == '-' || (c >= '0' && c <= '9'))
            {
                return ParseNumber(s, ref pos);
            }

            return null;
        }

        private static object ParseNumber(string s, ref int pos)
        {
            int start = pos;
            bool isFloat = false;
            if (pos < s.Length && s[pos] == '-') pos++;
            while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9') pos++;
            if (pos < s.Length && s[pos] == '.')
            {
                isFloat = true;
                pos++;
                while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9') pos++;
            }
            if (pos < s.Length && (s[pos] == 'e' || s[pos] == 'E'))
            {
                isFloat = true;
                pos++;
                if (pos < s.Length && (s[pos] == '+' || s[pos] == '-')) pos++;
                while (pos < s.Length && s[pos] >= '0' && s[pos] <= '9') pos++;
            }

            string numStr = s.Substring(start, pos - start);
            if (isFloat)
            {
                if (double.TryParse(numStr,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double dVal))
                    return dVal;
                return null;
            }
            else
            {
                if (long.TryParse(numStr,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out long lVal))
                {
                    if (lVal >= int.MinValue && lVal <= int.MaxValue)
                        return (int)lVal;
                    return lVal;
                }
                return null;
            }
        }

        /// <summary>Alternate parse that works on a substring.</summary>
        private static Dictionary<string, object> ParseSimpleJson(string json, out int consumed)
        {
            // Count leading whitespace
            int start = 0;
            while (start < json.Length && char.IsWhiteSpace(json[start])) start++;

            var result = new Dictionary<string, object>();
            int pos = start;
            if (pos >= json.Length || json[pos] != '{')
            {
                consumed = 0;
                return null;
            }
            pos++; // skip '{'

            while (pos < json.Length)
            {
                SkipWhitespace(json, ref pos);
                if (pos >= json.Length)
                {
                    consumed = 0;
                    return null;
                }
                if (json[pos] == '}')
                {
                    pos++;
                    consumed = pos;
                    return result;
                }

                // Parse key
                string key = ParseString(json, ref pos);
                if (key == null)
                {
                    consumed = 0;
                    return null;
                }

                SkipWhitespace(json, ref pos);
                if (pos >= json.Length || json[pos] != ':')
                {
                    consumed = 0;
                    return null;
                }
                pos++; // skip ':'

                SkipWhitespace(json, ref pos);
                object value = ParseValue(json, ref pos);

                result[key] = value;

                SkipWhitespace(json, ref pos);
                if (pos >= json.Length)
                {
                    consumed = 0;
                    return null;
                }
                if (json[pos] == '}')
                {
                    pos++;
                    consumed = pos;
                    return result;
                }
                if (json[pos] == ',')
                {
                    pos++;
                    continue;
                }
                consumed = 0;
                return null;
            }

            consumed = 0;
            return null;
        }
    }
}
