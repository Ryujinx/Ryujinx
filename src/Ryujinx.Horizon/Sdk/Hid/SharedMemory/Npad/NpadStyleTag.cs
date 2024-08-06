using System;

namespace Ryujinx.Horizon.Sdk.Hid
{
    /// <summary>
    /// Nintendo pad style
    /// </summary>
    [Flags]
    public enum NpadStyleTag : uint
    {
        /// <summary>
        /// No type.
        /// </summary>
        None = 0,

        /// <summary>
        /// Pro controller.
        /// </summary>
        FullKey = 1 << 0,

        /// <summary>
        /// Joy-Con controller in handheld mode.
        /// </summary>
        Handheld = 1 << 1,

        /// <summary>
        /// Joy-Con controller in dual mode.
        /// </summary>
        JoyDual = 1 << 2,

        /// <summary>
        /// Joy-Con left controller in single mode.
        /// </summary>
        JoyLeft = 1 << 3,

        /// <summary>
        /// Joy-Con right controller in single mode.
        /// </summary>
        JoyRight = 1 << 4,

        /// <summary>
        /// GameCube controller.
        /// </summary>
        Gc = 1 << 5,

        /// <summary>
        /// Poké Ball Plus controller.
        /// </summary>
        Palma = 1 << 6,

        /// <summary>
        /// NES and Famicom controller.
        /// </summary>
        Lark = 1 << 7,

        /// <summary>
        /// NES and Famicom controller in handheld mode.
        /// </summary>
        HandheldLark = 1 << 8,

        /// <summary>
        /// SNES controller.
        /// </summary>
        Lucia = 1 << 9,

        /// <summary>
        /// Generic external controller.
        /// </summary>
        SystemExt = 1 << 29,

        /// <summary>
        /// Generic controller.
        /// </summary>
        System = 1 << 30,
    }
}
