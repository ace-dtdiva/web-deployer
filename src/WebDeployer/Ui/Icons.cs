using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WebDeployer.Ui
{
    internal enum IconKind { None, Play, Stop, Refresh, Upload, Folder, Terminal }

    /// <summary>Icone a tratto, ridisegnate dalle icone SVG del design (griglia 24×24).</summary>
    internal static class Icons
    {
        public static void Draw(Graphics g, IconKind kind, RectangleF bounds, Color color)
        {
            if (kind == IconKind.None) return;

            var s = bounds.Width / 24f;
            PointF P(float x, float y) => new PointF(bounds.X + x * s, bounds.Y + y * s);

            using (var pen = new Pen(color, Math.Max(1f, 2f * s)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                switch (kind)
                {
                    case IconKind.Play:
                        g.DrawPolygon(pen, new[] { P(6, 4), P(20, 12), P(6, 20) });
                        break;
                    case IconKind.Stop:
                        g.DrawRectangle(pen, bounds.X + 6 * s, bounds.Y + 6 * s, 12 * s, 12 * s);
                        break;
                    case IconKind.Refresh:
                        g.DrawArc(pen, bounds.X + 3 * s, bounds.Y + 3 * s, 18 * s, 18 * s, 0, 315);
                        g.DrawLines(pen, new[] { P(21, 3), P(21, 9), P(15, 9) });
                        break;
                    case IconKind.Upload:
                        g.DrawLine(pen, P(12, 3), P(12, 15));
                        g.DrawLines(pen, new[] { P(7, 8), P(12, 3), P(17, 8) });
                        g.DrawLines(pen, new[] { P(4, 15), P(4, 19), P(6, 21), P(18, 21), P(20, 19), P(20, 15) });
                        break;
                    case IconKind.Folder:
                        g.DrawPolygon(pen, new[] { P(3, 7), P(5, 5), P(9, 5), P(11, 7), P(19, 7), P(21, 9), P(21, 17), P(19, 19), P(5, 19), P(3, 17) });
                        break;
                    case IconKind.Terminal:
                        g.DrawLines(pen, new[] { P(4, 17), P(10, 12), P(4, 7) });
                        g.DrawLine(pen, P(12, 19), P(20, 19));
                        break;
                }
            }
        }
    }
}
