using System;
using System.Runtime.InteropServices;

namespace NetworkToggle
{
    static class Glass
    {
        const int WindowCompositionAccentPolicy = 19;
        const int AccentEnableBlurBehind = 3;

        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll")]
        static extern int SetWindowCompositionAttribute(IntPtr window, ref WindowCompositionAttributeData data);

        [DllImport("gdi32.dll")]
        static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

        [DllImport("user32.dll")]
        static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);

        public static void BlurBehind(IntPtr window)
        {
            AccentPolicy accent = new AccentPolicy { AccentState = AccentEnableBlurBehind };
            int size = Marshal.SizeOf(accent);
            IntPtr pointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, pointer, false);
                WindowCompositionAttributeData data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAccentPolicy,
                    Data = pointer,
                    SizeOfData = size
                };
                SetWindowCompositionAttribute(window, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        public static void RoundCorners(IntPtr window, double width, double height, double radius, double scale)
        {
            int diameter = (int)Math.Round(radius * 2 * scale);
            IntPtr region = CreateRoundRectRgn(0, 0,
                (int)Math.Round(width * scale) + 1, (int)Math.Round(height * scale) + 1, diameter, diameter);
            SetWindowRgn(window, region, true);
        }
    }
}
