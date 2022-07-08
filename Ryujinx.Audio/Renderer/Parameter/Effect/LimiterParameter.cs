//
// Copyright (c) 2019-2021 Ryujinx
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
//

using Ryujinx.Audio.Renderer.Server.Effect;
using Ryujinx.Common.Memory;
using System.Runtime.InteropServices;

namespace Ryujinx.Audio.Renderer.Parameter.Effect
{
    /// <summary>
    /// <see cref="IEffectInParameter.SpecificData"/> for <see cref="Common.EffectType.Limiter"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LimiterParameter
    {
        /// <summary>
        /// The input channel indices that will be used by the <see cref="Dsp.AudioProcessor"/>.
        /// </summary>
        public Array6<byte> Input;

        /// <summary>
        /// The output channel indices that will be used by the <see cref="Dsp.AudioProcessor"/>.
        /// </summary>
        public Array6<byte> Output;

        /// <summary>
        /// The maximum number of channels supported.
        /// </summary>
        public ushort ChannelCountMax;

        /// <summary>
        /// The total channel count used.
        /// </summary>
        public ushort ChannelCount;

        /// <summary>
        /// The target sample rate.
        /// </summary>
        /// <remarks>This is in kHz.</remarks>
        public int SampleRate;

        /// <summary>
        /// The look ahead max time.
        /// <remarks>This is in microseconds.</remarks>
        /// </summary>
        public int LookAheadTimeMax;

        /// <summary>
        /// The attack time.
        /// <remarks>This is in microseconds.</remarks>
        /// </summary>
        public int AttackTime;

        /// <summary>
        /// The release time.
        /// <remarks>This is in microseconds.</remarks>
        /// </summary>
        public int ReleaseTime;

        /// <summary>
        /// The look ahead time.
        /// <remarks>This is in microseconds.</remarks>
        /// </summary>
        public int LookAheadTime;

        /// <summary>
        /// The attack coefficient.
        /// </summary>
        public float AttackCoefficient;

        /// <summary>
        /// The release coefficient.
        /// </summary>
        public float ReleaseCoefficient;

        /// <summary>
        /// The threshold.
        /// </summary>
        public float Threshold;

        /// <summary>
        /// The input gain.
        /// </summary>
        public float InputGain;

        /// <summary>
        /// The output gain.
        /// </summary>
        public float OutputGain;

        /// <summary>
        /// The minimum samples stored in the delay buffer.
        /// </summary>
        public int DelayBufferSampleCountMin;

        /// <summary>
        /// The maximum samples stored in the delay buffer.
        /// </summary>
        public int DelayBufferSampleCountMax;

        /// <summary>
        /// The current usage status of the effect on the client side.
        /// </summary>
        public UsageState Status;

        /// <summary>
        /// Indicate if the limiter effect should output statistics.
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool StatisticsEnabled;

        /// <summary>
        /// Indicate to the DSP that the user did a statistics reset.
        /// </summary>
        [MarshalAs(UnmanagedType.I1)]
        public bool StatisticsReset;

        /// <summary>
        /// Reserved/padding.
        /// </summary>
        private byte _reserved;

        /// <summary>
        /// Check if the <see cref="ChannelCount"/> is valid.
        /// </summary>
        /// <returns>Returns true if the <see cref="ChannelCount"/> is valid.</returns>
        public bool IsChannelCountValid()
        {
            return EffectInParameterVersion1.IsChannelCountValid(ChannelCount);
        }

        /// <summary>
        /// Check if the <see cref="ChannelCountMax"/> is valid.
        /// </summary>
        /// <returns>Returns true if the <see cref="ChannelCountMax"/> is valid.</returns>
        public bool IsChannelCountMaxValid()
        {
            return EffectInParameterVersion1.IsChannelCountValid(ChannelCountMax);
        }
    }
}
