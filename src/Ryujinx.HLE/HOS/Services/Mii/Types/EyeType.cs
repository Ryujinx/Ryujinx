﻿using System.Diagnostics.CodeAnalysis;

namespace Ryujinx.HLE.HOS.Services.Mii.Types
{
    [SuppressMessage("Design", "CA1069: Enums values should not be duplicated")]
    enum EyeType : byte
    {
        Normal,
        NormalLash,
        WhiteLash,
        WhiteNoBottom,
        OvalAngledWhite,
        AngryWhite,
        DotLashType1,
        Line,
        DotLine,
        OvalWhite,
        RoundedWhite,
        NormalShadow,
        CircleWhite,
        Circle,
        CircleWhiteStroke,
        NormalOvalNoBottom,
        NormalOvalLarge,
        NormalRoundedNoBottom,
        SmallLash,
        Small,
        TwoSmall,
        NormalLongLash,
        WhiteTwoLashes,
        WhiteThreeLashes,
        DotAngry,
        DotAngled,
        Oval,
        SmallWhite,
        WhiteAngledNoBottom,
        WhiteAngledNoLeft,
        SmallWhiteTwoLashes,
        LeafWhiteLash,
        WhiteLargeNoBottom,
        Dot,
        DotLashType2,
        DotThreeLashes,
        WhiteOvalTop,
        WhiteOvalBottom,
        WhiteOvalBottomFlat,
        WhiteOvalTwoLashes,
        WhiteOvalThreeLashes,
        WhiteOvalNoBottomTwoLashes,
        DotWhite,
        WhiteOvalTopFlat,
        WhiteThinLeaf,
        StarThreeLashes,
        LineTwoLashes,
        CrowsFeet,
        WhiteNoBottomFlat,
        WhiteNoBottomRounded,
        WhiteSmallBottomLine,
        WhiteNoBottomLash,
        WhiteNoPartialBottomLash,
        WhiteOvalBottomLine,
        WhiteNoBottomLashTopLine,
        WhiteNoPartialBottomTwoLashes,
        NormalTopLine,
        WhiteOvalLash,
        RoundTired,
        WhiteLarge,

        Min = 0,
        Max = 59,
    }
}
