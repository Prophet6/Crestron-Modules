using System;
using System.Globalization;
using System.Text;

namespace TeamsRoomHelpLib
{
    public static class HelpSchedule
    {
        public const int DefaultStartMinute = 8 * 60;
        public const int DefaultEndMinute = 17 * 60;

        public static bool TryParseClock(string text, out int minutes)
        {
            minutes = 0;
            if (text == null)
                return false;
            string trimmed = text.Trim();
            int colon = trimmed.IndexOf(':');
            if (colon <= 0 || colon != trimmed.LastIndexOf(':'))
                return false;
            string hourText = trimmed.Substring(0, colon);
            string minuteText = trimmed.Substring(colon + 1);
            if (hourText.Length < 1 || hourText.Length > 2)
                return false;
            if (minuteText.Length != 2)
                return false;
            int hour;
            int minute;
            if (!TryDigits(hourText, out hour) || !TryDigits(minuteText, out minute))
                return false;
            if (hour > 23 || minute > 59)
                return false;
            minutes = (hour * 60) + minute;
            return true;
        }

        public static bool IsWeekend(DateTime local)
        {
            return local.DayOfWeek == DayOfWeek.Saturday || local.DayOfWeek == DayOfWeek.Sunday;
        }

        public static bool PollAllowed(DateTime nowLocal, bool businessOnly, int startMinute, int endMinute, bool holidayToday)
        {
            if (!businessOnly)
                return true;
            if (endMinute <= startMinute)
                return false;
            if (holidayToday || IsWeekend(nowLocal))
                return false;
            int minute = (nowLocal.Hour * 60) + nowLocal.Minute;
            return minute >= startMinute && minute < endMinute;
        }

        public static bool TryClearAt(
            DateTime sentLocal,
            DateTime nowLocal,
            int clearHours,
            bool autoClear,
            bool businessOnly,
            int startMinute,
            int endMinute,
            bool holidayToday,
            out DateTime clearAt)
        {
            clearAt = DateTime.MinValue;
            if (!autoClear || clearHours < 1)
                return false;
            if (!businessOnly)
            {
                clearAt = sentLocal.AddHours(clearHours);
                return true;
            }
            if (endMinute <= startMinute)
                return false;

            int needSeconds = clearHours * 3600;
            DateTime holidayDate = nowLocal.Date;
            for (int day = 0; day < 400; day++)
            {
                DateTime date = sentLocal.Date.AddDays(day);
                if (IsWeekend(date))
                    continue;
                if (holidayToday && date.Date == holidayDate)
                    continue;
                DateTime open = date.AddMinutes(startMinute);
                DateTime close = date.AddMinutes(endMinute);
                DateTime from = sentLocal > open ? sentLocal : open;
                if (from >= close)
                    continue;
                int available = (int)(close - from).TotalSeconds;
                if (available <= 0)
                    continue;
                if (available >= needSeconds)
                {
                    clearAt = from.AddSeconds(needSeconds);
                    return true;
                }
                needSeconds -= available;
            }
            return false;
        }

        public static bool IsExpired(
            DateTime sentLocal,
            DateTime nowLocal,
            int clearHours,
            bool autoClear,
            bool businessOnly,
            int startMinute,
            int endMinute,
            bool holidayToday)
        {
            DateTime clearAt;
            if (!TryClearAt(sentLocal, nowLocal, clearHours, autoClear, businessOnly, startMinute, endMinute, holidayToday, out clearAt))
                return false;
            return nowLocal >= clearAt;
        }

        public static string FormatWhen(DateTime local, DateTime today)
        {
            string clock = local.ToString("h:mm tt", CultureInfo.InvariantCulture);
            if (local.Date == today.Date)
                return clock;
            return local.ToString("ddd", CultureInfo.InvariantCulture) + " " + clock;
        }

        public static string FormatReply(string isoUtc, DateTime nowLocal)
        {
            DateTime local;
            if (!TryParseUtc(isoUtc, out local))
                return "";
            return FormatWhen(local.ToLocalTime(), nowLocal);
        }

        public static bool TryParseUtc(string isoUtc, out DateTime utc)
        {
            utc = DateTime.MinValue;
            if (isoUtc == null || isoUtc.Trim().Length == 0)
                return false;
            DateTime parsed;
            if (!DateTime.TryParse(isoUtc.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                return false;
            if (parsed.Kind == DateTimeKind.Unspecified)
                parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            else if (parsed.Kind == DateTimeKind.Local)
                parsed = parsed.ToUniversalTime();
            utc = parsed;
            return true;
        }

        public static string ApplyTemplate(string template, string room, string issue)
        {
            if (template == null || template.Length == 0)
                return "";
            string withRoom = ReplaceToken(template, "{room}", room ?? "");
            return ReplaceToken(withRoom, "{issue}", issue ?? "");
        }

        public static string BuildObject(string[] keys, string[] values)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('{');
            bool any = false;
            int count = keys == null ? 0 : keys.Length;
            for (int i = 0; i < count; i++)
            {
                string key = keys[i];
                if (key == null || key.Length == 0)
                    continue;
                if (any)
                    sb.Append(',');
                any = true;
                sb.Append('"');
                sb.Append(Escape(key));
                sb.Append("\":\"");
                string value = values != null && i < values.Length ? values[i] : "";
                sb.Append(Escape(value));
                sb.Append('"');
            }
            sb.Append('}');
            return sb.ToString();
        }

        public static string ReadString(string json, string key)
        {
            string value;
            bool found;
            if (!TryReadString(json, key, out value, out found))
                return "";
            return value ?? "";
        }

        public static bool TryReadString(string json, string key, out string value, out bool found)
        {
            value = "";
            found = false;
            if (json == null || key == null || key.Length == 0)
                return false;
            string needle = "\"" + key + "\"";
            int from = 0;
            while (from < json.Length)
            {
                int hit = json.IndexOf(needle, from, StringComparison.Ordinal);
                if (hit < 0)
                    return false;
                int i = hit + needle.Length;
                i = SkipSpace(json, i);
                if (i >= json.Length || json[i] != ':')
                {
                    from = hit + needle.Length;
                    continue;
                }
                i = SkipSpace(json, i + 1);
                found = true;
                if (i >= json.Length)
                    return true;
                if (json[i] != '"')
                    return true;
                return ParseString(json, i + 1, out value);
            }
            return false;
        }

        public static string Escape(string value)
        {
            if (value == null || value.Length == 0)
                return "";
            StringBuilder sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static string ReplaceToken(string source, string token, string value)
        {
            int at = source.IndexOf(token, StringComparison.Ordinal);
            if (at < 0)
                return source;
            StringBuilder sb = new StringBuilder(source.Length + value.Length);
            int from = 0;
            while (at >= 0)
            {
                sb.Append(source, from, at - from);
                sb.Append(value);
                from = at + token.Length;
                at = source.IndexOf(token, from, StringComparison.Ordinal);
            }
            sb.Append(source, from, source.Length - from);
            return sb.ToString();
        }

        private static int SkipSpace(string json, int i)
        {
            while (i < json.Length)
            {
                char c = json[i];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                    break;
                i++;
            }
            return i;
        }

        private static bool ParseString(string json, int i, out string value)
        {
            StringBuilder sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '"')
                {
                    value = sb.ToString();
                    return true;
                }
                if (c == '\\')
                {
                    i++;
                    if (i >= json.Length)
                        break;
                    char esc = json[i];
                    if (esc == 'u')
                    {
                        if (i + 4 >= json.Length)
                            break;
                        int code;
                        if (!int.TryParse(json.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            break;
                        sb.Append((char)code);
                        i += 5;
                        continue;
                    }
                    switch (esc)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            sb.Append(esc);
                            break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                    i++;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            value = "";
            return false;
        }

        private static bool TryDigits(string text, out int number)
        {
            number = 0;
            if (text == null || text.Length == 0)
                return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < '0' || c > '9')
                    return false;
                number = (number * 10) + (c - '0');
            }
            return true;
        }
    }
}
