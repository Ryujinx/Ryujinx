using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GtkKey = Gdk.Key;

namespace Ryujinx.Input.GTK3
{
    public static class GTK3MappingHelper
    {
        private static readonly GtkKey[] _keyMapping = new GtkKey[(int)Key.Count]
        {
            // NOTE: invalid
            GtkKey.blank,

            GtkKey.Shift_L,
            GtkKey.Shift_R,
            GtkKey.Control_L,
            GtkKey.Control_R,
            GtkKey.Alt_L,
            GtkKey.Alt_R,
            GtkKey.Super_L,
            GtkKey.Super_R,
            GtkKey.Menu,
            GtkKey.F1,
            GtkKey.F2,
            GtkKey.F3,
            GtkKey.F4,
            GtkKey.F5,
            GtkKey.F6,
            GtkKey.F7,
            GtkKey.F8,
            GtkKey.F9,
            GtkKey.F10,
            GtkKey.F11,
            GtkKey.F12,
            GtkKey.F13,
            GtkKey.F14,
            GtkKey.F15,
            GtkKey.F16,
            GtkKey.F17,
            GtkKey.F18,
            GtkKey.F19,
            GtkKey.F20,
            GtkKey.F21,
            GtkKey.F22,
            GtkKey.F23,
            GtkKey.F24,
            GtkKey.F25,
            GtkKey.F26,
            GtkKey.F27,
            GtkKey.F28,
            GtkKey.F29,
            GtkKey.F30,
            GtkKey.F31,
            GtkKey.F32,
            GtkKey.F33,
            GtkKey.F34,
            GtkKey.F35,
            GtkKey.Up,
            GtkKey.Down,
            GtkKey.Left,
            GtkKey.Right,
            GtkKey.Return,
            GtkKey.Escape,
            GtkKey.space,
            GtkKey.Tab,
            GtkKey.BackSpace,
            GtkKey.Insert,
            GtkKey.Delete,
            GtkKey.Page_Up,
            GtkKey.Page_Down,
            GtkKey.Home,
            GtkKey.End,
            GtkKey.Caps_Lock,
            GtkKey.Scroll_Lock,
            GtkKey.Print,
            GtkKey.Pause,
            GtkKey.Num_Lock,
            GtkKey.Clear,
            GtkKey.KP_0,
            GtkKey.KP_1,
            GtkKey.KP_2,
            GtkKey.KP_3,
            GtkKey.KP_4,
            GtkKey.KP_5,
            GtkKey.KP_6,
            GtkKey.KP_7,
            GtkKey.KP_8,
            GtkKey.KP_9,
            GtkKey.KP_Divide,
            GtkKey.KP_Multiply,
            GtkKey.KP_Subtract,
            GtkKey.KP_Add,
            GtkKey.KP_Decimal,
            GtkKey.KP_Enter,
            GtkKey.a,
            GtkKey.b,
            GtkKey.c,
            GtkKey.d,
            GtkKey.e,
            GtkKey.f,
            GtkKey.g,
            GtkKey.h,
            GtkKey.i,
            GtkKey.j,
            GtkKey.k,
            GtkKey.l,
            GtkKey.m,
            GtkKey.n,
            GtkKey.o,
            GtkKey.p,
            GtkKey.q,
            GtkKey.r,
            GtkKey.s,
            GtkKey.t,
            GtkKey.u,
            GtkKey.v,
            GtkKey.w,
            GtkKey.x,
            GtkKey.y,
            GtkKey.z,
            GtkKey.Key_0,
            GtkKey.Key_1,
            GtkKey.Key_2,
            GtkKey.Key_3,
            GtkKey.Key_4,
            GtkKey.Key_5,
            GtkKey.Key_6,
            GtkKey.Key_7,
            GtkKey.Key_8,
            GtkKey.Key_9,
            GtkKey.grave,
            GtkKey.grave,
            GtkKey.minus,
            GtkKey.plus,
            GtkKey.bracketleft,
            GtkKey.bracketright,
            GtkKey.semicolon,
            GtkKey.quoteright,
            GtkKey.comma,
            GtkKey.period,
            GtkKey.slash,
            GtkKey.backslash,

            // NOTE: invalid
            GtkKey.blank,
        };

        private static readonly Dictionary<GtkKey, Key> _gtkKeyMapping;

        static GTK3MappingHelper()
        {
            var inputKeys = Enum.GetValues<Key>().SkipLast(1);

            // GtkKey is not contiguous and quite large, so use a dictionary instead of an array.
            _gtkKeyMapping = new Dictionary<GtkKey, Key>();

            foreach (var key in inputKeys)
            {
                var index = ToGtkKey(key);
                _gtkKeyMapping[index] = key;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GtkKey ToGtkKey(Key key)
        {
            return _keyMapping[(int)key];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Key ToInputKey(GtkKey key)
        {
            return _gtkKeyMapping.GetValueOrDefault(key, Key.Unknown);
        }
    }
}
