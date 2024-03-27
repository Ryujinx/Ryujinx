using Ryujinx.HLE.HOS.Applets;
using System.Text;
using Xunit;

namespace Ryujinx.Tests.HLE
{
    public class SoftwareKeyboardFacts
    {
        [Fact]
        public void StripUnicodeControlCodes_NullInput()
        {
            Assert.Null(SoftwareKeyboardApplet.StripUnicodeControlCodes(null));
        }

        [Fact]
        public void StripUnicodeControlCodes_EmptyInput()
        {
            Assert.Equal(string.Empty, SoftwareKeyboardApplet.StripUnicodeControlCodes(string.Empty));
        }

        [Fact]
        public void StripUnicodeControlCodes_Passthrough()
        {
            string[] prompts = {
                "Please name him.",
                "Name her, too.",
                "Name your friend.",
                "Name another friend.",
                "Name your pet.",
                "Favorite homemade food?",
                "What’s your favorite thing?",
                "Are you sure?",
            };

            foreach (string prompt in prompts)
            {
                Assert.Equal(prompt, SoftwareKeyboardApplet.StripUnicodeControlCodes(prompt));
            }
        }

        [Fact]
        public void StripUnicodeControlCodes_StripsNewlines()
        {
            Assert.Equal("I am very tall", SoftwareKeyboardApplet.StripUnicodeControlCodes("I \r\nam \r\nvery \r\ntall"));
        }

        [Fact]
        public void StripUnicodeControlCodes_StripsDeviceControls()
        {
            // 0x13 is control code DC3 used by some games
            string specialInput = Encoding.UTF8.GetString(new byte[] { 0x13, 0x53, 0x68, 0x69, 0x6E, 0x65, 0x13 });
            Assert.Equal("Shine", SoftwareKeyboardApplet.StripUnicodeControlCodes(specialInput));
        }

        [Fact]
        public void StripUnicodeControlCodes_StripsToEmptyString()
        {
            string specialInput = Encoding.UTF8.GetString(new byte[] { 17, 18, 19, 20 }); // DC1 - DC4 special codes
            Assert.Equal(string.Empty, SoftwareKeyboardApplet.StripUnicodeControlCodes(specialInput));
        }

        [Fact]
        public void StripUnicodeControlCodes_PreservesMultiCodePoints()
        {
            // Turtles are a good example of multi-codepoint Unicode chars
            string specialInput = "♀ 🐢 🐢 ♂ ";
            Assert.Equal(specialInput, SoftwareKeyboardApplet.StripUnicodeControlCodes(specialInput));
        }
    }
}
