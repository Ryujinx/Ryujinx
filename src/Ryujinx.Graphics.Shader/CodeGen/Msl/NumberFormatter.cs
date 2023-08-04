using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Globalization;

namespace Ryujinx.Graphics.Shader.CodeGen.Msl
{
    static class NumberFormatter
    {
        private const int MaxDecimal = 256;

        public static bool TryFormat(int value, AggregateType dstType, out string formatted)
        {
            if (dstType == AggregateType.FP32)
            {
                return TryFormatFloat(BitConverter.Int32BitsToSingle(value), out formatted);
            }
            else if (dstType == AggregateType.S32)
            {
                formatted = FormatInt(value);
            }
            else if (dstType == AggregateType.U32)
            {
                formatted = FormatUint((uint)value);
            }
            else if (dstType == AggregateType.Bool)
            {
                formatted = value != 0 ? "true" : "false";
            }
            else
            {
                throw new ArgumentException($"Invalid variable type \"{dstType}\".");
            }

            return true;
        }

        public static string FormatFloat(float value)
        {
            if (!TryFormatFloat(value, out string formatted))
            {
                throw new ArgumentException("Failed to convert float value to string.");
            }

            return formatted;
        }

        public static bool TryFormatFloat(float value, out string formatted)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                formatted = null;

                return false;
            }

            formatted = value.ToString("F9", CultureInfo.InvariantCulture);

            if (!formatted.Contains('.'))
            {
                formatted += ".0f";
            }

            return true;
        }

        public static string FormatInt(int value, AggregateType dstType)
        {
            if (dstType == AggregateType.S32)
            {
                return FormatInt(value);
            }
            else if (dstType == AggregateType.U32)
            {
                return FormatUint((uint)value);
            }
            else
            {
                throw new ArgumentException($"Invalid variable type \"{dstType}\".");
            }
        }

        public static string FormatInt(int value)
        {
            if (value <= MaxDecimal && value >= -MaxDecimal)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }

            return "0x" + value.ToString("X", CultureInfo.InvariantCulture);
        }

        public static string FormatUint(uint value)
        {
            if (value <= MaxDecimal && value >= 0)
            {
                return value.ToString(CultureInfo.InvariantCulture) + "u";
            }

            return "0x" + value.ToString("X", CultureInfo.InvariantCulture) + "u";
        }
    }
}
