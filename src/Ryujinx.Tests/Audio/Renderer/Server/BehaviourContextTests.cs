using Ryujinx.Audio.Renderer.Server;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class BehaviourContextTests
    {
        [Fact]
        public void TestCheckFeature()
        {
            int latestRevision = BehaviourContext.BaseRevisionMagic + BehaviourContext.LastRevision;
            int previousRevision = BehaviourContext.BaseRevisionMagic + (BehaviourContext.LastRevision - 1);
            int invalidRevision = BehaviourContext.BaseRevisionMagic + (BehaviourContext.LastRevision + 1);

            Assert.True(BehaviourContext.CheckFeatureSupported(latestRevision, latestRevision));
            Assert.False(BehaviourContext.CheckFeatureSupported(previousRevision, latestRevision));
            Assert.True(BehaviourContext.CheckFeatureSupported(latestRevision, previousRevision));
            // In case we get an invalid revision, this is supposed to auto default to REV1 internally.. idk what the hell Nintendo was thinking here..
            Assert.True(BehaviourContext.CheckFeatureSupported(invalidRevision, latestRevision));
        }

        [Fact]
        public void TestsMemoryPoolForceMappingEnabled()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision1);

            Assert.False(behaviourContext.IsMemoryPoolForceMappingEnabled());

            behaviourContext.UpdateFlags(0x1);

            Assert.True(behaviourContext.IsMemoryPoolForceMappingEnabled());
        }

        [Fact]
        public void TestRevision1()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision1);

            Assert.False(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.False(behaviourContext.IsSplitterSupported());
            Assert.False(behaviourContext.IsLongSizePreDelaySupported());
            Assert.False(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.False(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.False(behaviourContext.IsSplitterBugFixed());
            Assert.False(behaviourContext.IsElapsedFrameCountSupported());
            Assert.False(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.False(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.False(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.False(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.False(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.False(behaviourContext.IsBiquadFilterGroupedOptimizationSupported());

            Assert.Equal(0.70f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.Equal(1, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.Equal(1u, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Fact]
        public void TestRevision2()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision2);

            Assert.True(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.True(behaviourContext.IsSplitterSupported());
            Assert.False(behaviourContext.IsLongSizePreDelaySupported());
            Assert.False(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.False(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.False(behaviourContext.IsSplitterBugFixed());
            Assert.False(behaviourContext.IsElapsedFrameCountSupported());
            Assert.False(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.False(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.False(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.False(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.False(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.False(behaviourContext.IsBiquadFilterGroupedOptimizationSupported());

            Assert.Equal(0.70f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.Equal(1, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.Equal(1u, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Fact]
        public void TestRevision3()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision3);

            Assert.True(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.True(behaviourContext.IsSplitterSupported());
            Assert.True(behaviourContext.IsLongSizePreDelaySupported());
            Assert.False(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.False(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.False(behaviourContext.IsSplitterBugFixed());
            Assert.False(behaviourContext.IsElapsedFrameCountSupported());
            Assert.False(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.False(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.False(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.False(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.False(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.False(behaviourContext.IsBiquadFilterGroupedOptimizationSupported());

            Assert.Equal(0.70f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.Equal(1, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.Equal(1u, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Fact]
        public void TestRevision4()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision4);

            Assert.True(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.True(behaviourContext.IsSplitterSupported());
            Assert.True(behaviourContext.IsLongSizePreDelaySupported());
            Assert.True(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.False(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.False(behaviourContext.IsSplitterBugFixed());
            Assert.False(behaviourContext.IsElapsedFrameCountSupported());
            Assert.False(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.False(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.False(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.False(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.False(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.False(behaviourContext.IsBiquadFilterGroupedOptimizationSupported());

            Assert.Equal(0.75f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.Equal(1, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.Equal(1u, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Fact]
        public void TestRevision5()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision5);

            Assert.True(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.True(behaviourContext.IsSplitterSupported());
            Assert.True(behaviourContext.IsLongSizePreDelaySupported());
            Assert.True(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.True(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.True(behaviourContext.IsSplitterBugFixed());
            Assert.True(behaviourContext.IsElapsedFrameCountSupported());
            Assert.True(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.False(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.False(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.False(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.False(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.False(behaviourContext.IsBiquadFilterGroupedOptimizationSupported());

            Assert.Equal(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.Equal(2, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.Equal(2u, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Fact]
        public void TestRevision6()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision6);

            Assert.True(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.True(behaviourContext.IsSplitterSupported());
            Assert.True(behaviourContext.IsLongSizePreDelaySupported());
            Assert.True(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.True(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.True(behaviourContext.IsSplitterBugFixed());
            Assert.True(behaviourContext.IsElapsedFrameCountSupported());
            Assert.True(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.True(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.False(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.False(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.False(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.False(behaviourContext.IsBiquadFilterGroupedOptimizationSupported());

            Assert.Equal(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.Equal(2, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.Equal(2u, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Fact]
        public void TestRevision7()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision7);

            Assert.True(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.True(behaviourContext.IsSplitterSupported());
            Assert.True(behaviourContext.IsLongSizePreDelaySupported());
            Assert.True(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.True(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.True(behaviourContext.IsSplitterBugFixed());
            Assert.True(behaviourContext.IsElapsedFrameCountSupported());
            Assert.True(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.True(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.True(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.False(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.False(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.False(behaviourContext.IsBiquadFilterGroupedOptimizationSupported());

            Assert.Equal(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.Equal(2, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.Equal(2u, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Fact]
        public void TestRevision8()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision8);

            Assert.True(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.True(behaviourContext.IsSplitterSupported());
            Assert.True(behaviourContext.IsLongSizePreDelaySupported());
            Assert.True(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.True(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.True(behaviourContext.IsSplitterBugFixed());
            Assert.True(behaviourContext.IsElapsedFrameCountSupported());
            Assert.True(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.True(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.True(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.True(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.False(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.False(behaviourContext.IsBiquadFilterGroupedOptimizationSupported());

            Assert.Equal(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.Equal(3, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.Equal(2u, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Fact]
        public void TestRevision9()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision9);

            Assert.True(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.True(behaviourContext.IsSplitterSupported());
            Assert.True(behaviourContext.IsLongSizePreDelaySupported());
            Assert.True(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.True(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.True(behaviourContext.IsSplitterBugFixed());
            Assert.True(behaviourContext.IsElapsedFrameCountSupported());
            Assert.True(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.True(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.True(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.True(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.True(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.False(behaviourContext.IsBiquadFilterGroupedOptimizationSupported());

            Assert.Equal(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.Equal(3, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.Equal(2u, behaviourContext.GetPerformanceMetricsDataFormat());
        }

        [Fact]
        public void TestRevision10()
        {
            BehaviourContext behaviourContext = new();

            behaviourContext.SetUserRevision(BehaviourContext.BaseRevisionMagic + BehaviourContext.Revision10);

            Assert.True(behaviourContext.IsAdpcmLoopContextBugFixed());
            Assert.True(behaviourContext.IsSplitterSupported());
            Assert.True(behaviourContext.IsLongSizePreDelaySupported());
            Assert.True(behaviourContext.IsAudioUsbDeviceOutputSupported());
            Assert.True(behaviourContext.IsFlushVoiceWaveBuffersSupported());
            Assert.True(behaviourContext.IsSplitterBugFixed());
            Assert.True(behaviourContext.IsElapsedFrameCountSupported());
            Assert.True(behaviourContext.IsDecodingBehaviourFlagSupported());
            Assert.True(behaviourContext.IsBiquadFilterEffectStateClearBugFixed());
            Assert.True(behaviourContext.IsMixInParameterDirtyOnlyUpdateSupported());
            Assert.True(behaviourContext.IsWaveBufferVersion2Supported());
            Assert.True(behaviourContext.IsEffectInfoVersion2Supported());
            Assert.True(behaviourContext.IsBiquadFilterGroupedOptimizationSupported());

            Assert.Equal(0.80f, behaviourContext.GetAudioRendererProcessingTimeLimit());
            Assert.Equal(4, behaviourContext.GetCommandProcessingTimeEstimatorVersion());
            Assert.Equal(2u, behaviourContext.GetPerformanceMetricsDataFormat());
        }
    }
}
