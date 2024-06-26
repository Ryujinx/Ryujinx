using LibHac.FsSystem;
using Microsoft.FSharp.Core;
using Microsoft.VisualBasic;
using NUnit.Framework;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Graphics.Texture.Astc;
using Ryujinx.HLE.HOS.Services.SurfaceFlinger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ryujinx.Tests.Graphics
{
    /**
     * 
     * NOTES
     * 
     * HOW I GENERATED INPUT DATA
     * Step 1. Create ASTC-compressed image from sample data.
     * *** NOTE This image isn't 2^x evenly sized, it's 768x512 - something that would break down into nice squares, for sure. 
     *     `astcenc-sse2 -cl MoreRocks.png MoreRocks.l-4x4-100.astc 4x4 100`
     * 
     * Step 2. Decompress the data we just created. 
     *     `astcenc-sse2 -dl MoreRocks.l-4x4-100.astc MoreRocks.l-4x4-100.astc.png`
     *     
     * Step 3. 
     *     I used convertio to convert the PNG generated in step 2 to create MorRocks.l-4x4-100.astc.rgba
     
     * WHAT WE DO IN THE TEST BELOW:
     * 1. Read the sample image, ASTC-compressed reference, and decompressed reference that we generated above.
     * 2. Run TryDecodeToRgba8P on our ASTC-compressed texture.
     * 2a. Write the output of step 2 to the disk.
     * 3. Assert that the data we decompressed in our method is the same data as the decompressed reference image.
     * 
     */

    public class AstcDecoderTests
    {
        private string _workingDir;
        private string _testDataDir;

        [SetUp]
        public void SetupFixture()
        {
            _workingDir = TestContext.CurrentContext.TestDirectory;
            _testDataDir = Path.Join(_workingDir, "Graphics", "TestData");
            GraphicsConfig.EnableTextureRecompression = false;
        }

        // public void 
        [TestCase(4, 4)]
        [TestCase(5, 5)]
        [TestCase(5, 4)]
        [TestCase(6, 5)]
        [TestCase(6, 6)]
        [TestCase(8, 5)]
        [TestCase(8, 6)]
        [TestCase(8, 8)]
        [TestCase(10, 5)]
        [TestCase(10, 6)]
        [TestCase(10, 8)]
        [TestCase(10, 10)]
        [TestCase(12, 10)]
        [TestCase(12, 12)]
        public void Paramterized_BlockSizes_Test(int blockWidth, int blockHeight)
        {
            TestContext.Out.WriteLine($"Testing Block Size {blockWidth}x{blockHeight}");
            var (encodedRef, decodedRef) = GetTestDataTupleFromShortname("MoreRocks", blockWidth, blockHeight);
            int astcHeaderLength = 16;

            // skip the header. Decode method doesn't work without this and will return false.
            var rawastc = encodedRef[astcHeaderLength..];

            int texWidth = 256;
            int texHeight = 256;
            byte[] outputBuffer = Array.Empty<byte>();

            int depth = 1;
            int levels = 1;
            int layers = 1;

            bool succeeded = AstcDecoder.TryDecodeToRgba8P(rawastc, blockWidth, blockHeight, texWidth, texHeight, depth, levels, layers, out outputBuffer);

            // The decode function said it was valid data and that it could parse it.
            Assert.AreEqual(true, succeeded);
            // Length is the same as the one we made w/ ARM's decoder. That's good.
            Assert.AreEqual(decodedRef.Length, outputBuffer.Length);

            var wordsRef = RgbaWord.FromBytes(decodedRef.ToArray());
            var wordsOut = RgbaWord.FromBytes(outputBuffer);
            var wordDifferences = wordsRef.Select((x, i) => new { index = i, diff = x.Diff(wordsOut[i]) }).ToArray();

            // BUT compression is funny.
            // Calculate the byte differences. 
            var byteDifferences = decodedRef.ToArray().Select((x, i) => new { index = i, delta = x - outputBuffer[i] }).ToList();

            var matchCount = byteDifferences.Count(x => x.delta == 0);
            var matchPercent = ((float)matchCount / outputBuffer.Length);

            var wordUnchangedCount = wordDifferences.Count(x => x.diff.IsZero());
            var wordUnchangedPercent = (float)wordUnchangedCount / wordDifferences.Length;

            TestContext.Out.WriteLine($"Pixel-wise comparison: {wordUnchangedPercent * 100:F4} ({wordUnchangedCount}/{wordDifferences.Length})");
            TestContext.Out.WriteLine($"Byte-wise comparison: {matchPercent * 100:F4} ({matchCount}/{byteDifferences.Count}) were same.");

            for (var threshold = 1; threshold < 32; threshold++)
            {
                var tc = byteDifferences.Count(x => Math.Abs(x.delta) >= threshold);
                var tcp = ((float)tc / byteDifferences.Count);
                if (tc > 0)
                    TestContext.Out.WriteLine($"{tcp * 100:F4}% ({tc}/{byteDifferences.Count}) are different by at least {threshold}.");
            }

            Assert.IsTrue(byteDifferences.All(x => Math.Abs(x.delta) <= 1));
        }

        /// <summary>
        /// Get test data from FS using short name naming convention.
        /// </summary>
        /// <param name="shortName"></param>
        /// <returns></returns>
        private (ReadOnlyMemory<byte>, ReadOnlyMemory<byte>) GetTestDataTupleFromShortname(string shortName, int blockWidth, int blockHeight)
        {
            var encodedRef = GetFileDataFromPath($"{shortName}.l-{blockWidth}x{blockHeight}-100.astc");
            // var decodedRef = _getFileDataFromPath($"{shortName}.s4x4.astc.png");
            var rgba8raw = GetFileDataFromPath($"{shortName}.l-{blockWidth}x{blockHeight}-100.astc.rgba");

            return (encodedRef, rgba8raw);
        }

        private ReadOnlyMemory<byte> GetFileDataFromPath(string relativeFilePath)
        {
            var fullPath = Path.Join(_testDataDir, relativeFilePath);
            return File.ReadAllBytes(fullPath);
        }

        private class RgbaWord
        {
            public byte r;
            public byte g;
            public byte b;
            public byte a;

            public bool IsZero()
            {
                return r == 0 && g == 0 && b == 0 && a == 0;
            }

            public bool SameAs(RgbaWord other)
            {
                return this.r == other.r && this.g == other.g && this.b == other.b && this.a == other.a;
            }

            public RgbaWord Diff(RgbaWord other)
            {
                /*
                
                    Returns 0 for a field if equal and absolute value of diff if not.
                   

                 */
                return new RgbaWord()
                {
                    r = (byte)Math.Abs(this.r - other.r),
                    g = (byte)Math.Abs(this.g - other.g),
                    b = (byte)Math.Abs(this.b - other.b),
                    a = (byte)Math.Abs(this.a - other.a)
                };
            }

            /// <summary>
            /// Return an array of RGBA words given an array of bytes.        
            /// </summary>
            /// <param name="rawBytes"></param>
            /// <returns></returns>
            public static RgbaWord[] FromBytes(byte[] rawBytes)
            {
                var result = new List<RgbaWord>();
                // rawbytes has to be factor-of-4-sized. 
                for (var i = 0; i < rawBytes.Length; i += 4)
                {
                    result.Add(new RgbaWord()
                    {
                        r = rawBytes[i],
                        g = rawBytes[i + 1],
                        b = rawBytes[i + 2],
                        a = rawBytes[i + 3]
                    });
                }
                return result.ToArray();
            }
        }
    }
}
