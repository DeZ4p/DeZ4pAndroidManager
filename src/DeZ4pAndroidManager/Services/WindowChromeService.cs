// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DeZ4pAndroidManager.Services;

/// <summary>
/// Enables the native dark title bar on Windows 10 (build 1809+) and Windows 11.
/// Uses DwmSetWindowAttribute with DWMWA_USE_IMMERSIVE_DARK_MODE.
/// Falls back silently on older Windows versions.
/// </summary>
public static class WindowChromeService
{
    // Attribute IDs: 19 on Win10 1809+, 20 on newer builds
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    // Caption color attributes (Win11 only, ignored gracefully on Win10)
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left, Right, Top, Bottom;
    }

    /// <summary>
    /// Applies dark or light mode to the given Window's native title bar.
    /// </summary>
    /// <param name="window">The WPF window.</param>
    /// <param name="isDark">True for dark, false for light.</param>
    /// <param name="captionColorHex">Optional caption color in 0x00BBGGRR (Win11 only). Pass 0 to skip.</param>
    public static void ApplyTitleBarTheme(Window window, bool isDark, int captionColorHex = 0)
    {
        if (window == null) return;

        try
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.Handle;

            // If Handle is zero, window not yet created - hook into SourceInitialized
            if (hwnd == IntPtr.Zero)
            {
                window.SourceInitialized += (_, _) => ApplyTitleBarTheme(window, isDark, captionColorHex);
                return;
            }

            int useDark = isDark ? 1 : 0;

            // Try attribute 20 first (newer builds), then 19 (older Win10 1809)
            int result = DwmSetWindowAttribute(hwnd,
                DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref useDark,
                sizeof(int));

            if (result != 0)
            {
                DwmSetWindowAttribute(hwnd,
                    DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1,
                    ref useDark,
                    sizeof(int));
            }

            // Win11 caption + text colors - ignored on Win10
            if (captionColorHex != 0)
            {
                int caption = captionColorHex;
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));

                int text = isDark ? 0x00FFFFFF : 0x00000000; // ABGR: white or black
                DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
            }
        }
        catch
        {
            // DWM not available - nothing we can do, fail silently
        }
    }
}