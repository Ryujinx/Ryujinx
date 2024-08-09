using ARMeilleure.Common;
using ARMeilleure.Translation;
using NUnit.Framework;
using Ryujinx.Cpu.Jit;
using Ryujinx.Tests.Memory;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Tests.Cpu
{
    internal class EnvironmentTests
    {
#pragma warning disable IDE0052 // Remove unread private member
        private static Translator _translator;
#pragma warning restore IDE0052

        private static void EnsureTranslator()
        {
            // Create a translator, as one is needed to register the signal handler or emit methods.
            _translator ??= new Translator(
                new JitMemoryAllocator(),
                new MockMemoryManager(),
                AddressTable<ulong>.CreateForArm(true, ARMeilleure.Memory.MemoryManagerType.SoftwarePageTable));
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static float GetDenormal()
        {
            return BitConverter.Int32BitsToSingle(1);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static float GetZero()
        {
            return BitConverter.Int32BitsToSingle(0);
        }

        /// <summary>
        /// This test ensures that managed methods do not reset floating point control flags.
        /// This is used to avoid changing control flags when running methods that don't require it, such as SVC calls, software memory...
        /// </summary>
        [Test]
        public void FpFlagsPInvoke()
        {
            EnsureTranslator();

            // Subnormal results are not flushed to zero by default.
            // This operation should not be allowed to do constant propagation, hence the methods that explicitly disallow inlining.
            Assert.AreNotEqual(GetDenormal() + GetZero(), 0f);

            bool methodCalled = false;
            bool isFz = false;

            var method = TranslatorTestMethods.GenerateFpFlagsPInvokeTest();

            // This method sets flush-to-zero and then calls the managed method.
            // Before and after setting the flags, it ensures subnormal addition works as expected.
            // It returns a positive result if any tests fail, and 0 on success (or if the platform cannot change FP flags)
            int result = method(Marshal.GetFunctionPointerForDelegate(ManagedMethod));

            // Subnormal results are not flushed to zero by default, which we should have returned to exiting the method.
            Assert.AreNotEqual(GetDenormal() + GetZero(), 0f);

            Assert.True(result == 0);
            Assert.True(methodCalled);
            Assert.True(isFz);
            return;

            void ManagedMethod()
            {
                // Floating point math should not modify fp flags.
                float test = 2f * 3.5f;

                if (test < 4f)
                {
                    throw new Exception("Sanity check.");
                }

                isFz = GetDenormal() + GetZero() == 0f;

                try
                {
                    if (test >= 4f)
                    {
                        throw new Exception("Always throws.");
                    }
                }
                catch
                {
                    // Exception handling should not modify fp flags.

                    methodCalled = true;
                }
            }
        }
    }
}
