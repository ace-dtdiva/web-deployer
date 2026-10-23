using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WebDeployer.Ui
{
    /// <summary>Colori e caratteri del design "WebDeployer – redesign".</summary>
    internal static class Theme
    {
        public static readonly Color Background = Hex(0xF5F4F9);
        public static readonly Color Surface = Color.White;
        public static readonly Color SurfaceMuted = Hex(0xF8F7FB);   // campi di sola lettura, pulsanti disattivati
        public static readonly Color Border = Hex(0xE4E2EC);
        public static readonly Color InputBorder = Hex(0xCFCBDB);
        public static readonly Color Divider = Hex(0xEEECF3);
        public static readonly Color Text = Hex(0x1C1A24);
        public static readonly Color TextLabel = Hex(0x3E3A4A);
        public static readonly Color TextMuted = Hex(0x5E5A6B);
        public static readonly Color TextDisabled = Hex(0x8D8999);
        public static readonly Color Accent = Hex(0x5B3FD6);
        public static readonly Color AccentHover = Hex(0x4A31BF);
        public static readonly Color AccentPressed = Hex(0x3F2A9E);
        public static readonly Color AccentSoft = Hex(0xEEEAFD);
        public static readonly Color AccentDisabled = Hex(0xC9BFF2);
        public static readonly Color NavBackground = Hex(0xF0EEF6);
        public static readonly Color HoverFill = Hex(0xF5F4F9);
        public static readonly Color PressedFill = Hex(0xECEAF3);
        public static readonly Color Success = Hex(0x2E9E5B);
        public static readonly Color SuccessSoft = Hex(0xEAF6EF);
        public static readonly Color SuccessText = Hex(0x1D6B3E);
        public static readonly Color Danger = Hex(0xC0392B);
        public static readonly Color DangerSoft = Hex(0xFCECEA);
        public static readonly Color DangerText = Hex(0x9B2C1F);
        public static readonly Color Idle = Hex(0xA9A5B5);

        // Pannello del log (scuro)
        public static readonly Color LogBackground = Hex(0x17161C);
        public static readonly Color LogBorder = Hex(0x2B2933);
        public static readonly Color LogTitle = Hex(0xEDEBF3);
        public static readonly Color LogText = Hex(0xD3D0DC);
        public static readonly Color LogMuted = Hex(0x8D8999);
        public static readonly Color DarkButton = Hex(0x24222B);
        public static readonly Color DarkButtonHover = Hex(0x2E2B37);
        public static readonly Color DarkButtonBorder = Hex(0x3A3744);
        public static readonly Color DarkDisabledText = Hex(0x6F6B7C);

        // Le misure del design sono in px CSS: a 96 DPI 1 px = 0,75 pt
        public static Font Regular(float px) => new Font("Segoe UI", px * 0.75f);
        public static Font Semibold(float px) => new Font("Segoe UI Semibold", px * 0.75f);
        public static Font Bold(float px) => new Font("Segoe UI", px * 0.75f, FontStyle.Bold);
        public static Font Mono(float px) => new Font("Consolas", px * 0.75f);

        /// <summary>Fattore di scala dello schermo (1 a 96 DPI, 1,5 a 144 DPI).</summary>
        public static float Scale(Control control) => control.DeviceDpi / 96f;

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }
}
