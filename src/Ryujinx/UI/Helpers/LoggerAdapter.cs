using Avalonia.Logging;
using Avalonia.Utilities;
using Ryujinx.Common.Logging;
using System;
using System.Text;

namespace Ryujinx.Ava.UI.Helpers
{
    using AvaLogger = Avalonia.Logging.Logger;
    using AvaLogLevel = LogEventLevel;
    using RyuLogClass = LogClass;
    using RyuLogger = Ryujinx.Common.Logging.Logger;

    internal class LoggerAdapter : ILogSink
    {
        public static void Register()
        {
            AvaLogger.Sink = new LoggerAdapter();
        }

        private static RyuLogger.Log? GetLog(AvaLogLevel level)
        {
            return level switch
            {
                AvaLogLevel.Verbose => RyuLogger.Debug,
                AvaLogLevel.Debug => RyuLogger.Debug,
                AvaLogLevel.Information => RyuLogger.Debug,
                AvaLogLevel.Warning => RyuLogger.Debug,
                AvaLogLevel.Error => RyuLogger.Error,
                AvaLogLevel.Fatal => RyuLogger.Error,
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
            };
        }

        public bool IsEnabled(AvaLogLevel level, string area)
        {
            return GetLog(level) != null;
        }

        public void Log(AvaLogLevel level, string area, object source, string messageTemplate)
        {
            GetLog(level)?.PrintMsg(RyuLogClass.UI, Format(level, area, messageTemplate, source, null));
        }

        public void Log(AvaLogLevel level, string area, object source, string messageTemplate, params object[] propertyValues)
        {
            GetLog(level)?.PrintMsg(RyuLogClass.UI, Format(level, area, messageTemplate, source, propertyValues));
        }

        private static string Format(AvaLogLevel level, string area, string template, object source, object[] v)
        {
            var result = new StringBuilder();
            var r = new CharacterReader(template.AsSpan());
            int i = 0;

            result.Append('[');
            result.Append(level);
            result.Append("] ");

            result.Append('[');
            result.Append(area);
            result.Append("] ");

            while (!r.End)
            {
                var c = r.Take();

                if (c != '{')
                {
                    result.Append(c);
                }
                else
                {
                    if (r.Peek != '{')
                    {
                        result.Append('\'');
                        result.Append(i < v.Length ? v[i++] : null);
                        result.Append('\'');
                        r.TakeUntil('}');
                        r.Take();
                    }
                    else
                    {
                        result.Append('{');
                        r.Take();
                    }
                }
            }

            if (source != null)
            {
                result.Append(" (");
                result.Append(source.GetType().Name);
                result.Append(" #");
                result.Append(source.GetHashCode());
                result.Append(')');
            }

            return result.ToString();
        }
    }
}
