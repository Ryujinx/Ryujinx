using Silk.NET.Vulkan;
using System;
using System.Diagnostics;

namespace Ryujinx.Graphics.Vulkan
{
    class ResourceArray : IDisposable
    {
        private DescriptorSet[] _cachedDescriptorSets;

        private ShaderCollection _cachedDscProgram;
        private int _cachedDscSetIndex;
        private int _cachedDscIndex;

        private int _bindCount;

        protected void SetDirty(VulkanRenderer gd, bool isImage)
        {
            ReleaseDescriptorSet();

            if (_bindCount != 0)
            {
                if (isImage)
                {
                    gd.PipelineInternal.ForceImageDirty();
                }
                else
                {
                    gd.PipelineInternal.ForceTextureDirty();
                }
            }
        }

        public bool TryGetCachedDescriptorSets(CommandBufferScoped cbs, ShaderCollection program, int setIndex, out DescriptorSet[] sets)
        {
            if (_cachedDescriptorSets != null)
            {
                _cachedDscProgram.UpdateManualDescriptorSetCollectionOwnership(cbs, _cachedDscSetIndex, _cachedDscIndex);

                sets = _cachedDescriptorSets;

                return true;
            }

            var dsc = program.GetNewManualDescriptorSetCollection(cbs, setIndex, out _cachedDscIndex).Get(cbs);

            sets = dsc.GetSets();

            _cachedDescriptorSets = sets;
            _cachedDscProgram = program;
            _cachedDscSetIndex = setIndex;

            return false;
        }

        public void IncrementBindCount()
        {
            _bindCount++;
        }

        public void DecrementBindCount()
        {
            int newBindCount = --_bindCount;
            Debug.Assert(newBindCount >= 0);
        }

        private void ReleaseDescriptorSet()
        {
            if (_cachedDescriptorSets != null)
            {
                _cachedDscProgram.ReleaseManualDescriptorSetCollection(_cachedDscSetIndex, _cachedDscIndex);
                _cachedDescriptorSets = null;
            }
        }

        public void Dispose()
        {
            ReleaseDescriptorSet();
        }
    }
}
