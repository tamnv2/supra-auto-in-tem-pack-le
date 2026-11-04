using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DriveSyncPortable;

internal static class AppIcon
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create()
    {
        using var bitmap = new Bitmap(64, 64);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            using var background = new SolidBrush(Theme.Blue);
            using (var shape = Theme.Rounded(new Rectangle(2, 2, 60, 60), 16))
                graphics.FillPath(background, shape);

            using var white = new Pen(Color.White, 5.2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawArc(white, 16, 16, 32, 32, -55, 285);
            using var whiteBrush = new SolidBrush(Color.White);
            graphics.FillPolygon(whiteBrush, new[]
            {
                new PointF(48, 14), new PointF(52, 30), new PointF(36, 25)
            });
        }

        IntPtr iconHandle = bitmap.GetHicon();
        try
        {
            using var wrapper = Icon.FromHandle(iconHandle);
            return (Icon)wrapper.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }
}
