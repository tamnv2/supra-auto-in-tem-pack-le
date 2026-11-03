using System.Drawing.Imaging;

namespace DriveSyncPortable;

internal static class LayoutAudit
{
    public static int Run(string outputDirectory)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);
            foreach (var (width, height) in new[] { (1664, 910), (970, 838), (825, 720) })
            {
                using var window = new MainForm
                {
                    ClientSize = new Size(width, height),
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(0, 0)
                };
                window.Show();
                Application.DoEvents();
                window.PerformLayout();
                window.Refresh();
                Application.DoEvents();

                var cards = GetDescendants(window).OfType<RoundedPanel>().ToArray();
                if (cards.Length < 8)
                    throw new InvalidOperationException("Expected 8 rounded cards, got " + cards.Length);
                foreach (var card in cards)
                {
                    if (card.Region is null)
                        throw new InvalidOperationException("Rounded clipping region missing");
                    if (card.BackColor != card.FillColor)
                        throw new InvalidOperationException("Card background differs from real BackColor");
                    if (card.Width < 140 || card.Height < 60)
                        throw new InvalidOperationException("Undersized card " + card.Bounds);
                    foreach (var label in card.Controls.OfType<Label>())
                    {
                        if (label.BackColor == Color.Transparent)
                            throw new InvalidOperationException("Transparent label caused broken corners: " + label.Text);
                        if (label.Right > card.ClientSize.Width - 4 || label.Bottom > card.ClientSize.Height - 3)
                            throw new InvalidOperationException("Clipped card content: " + label.Text);
                    }
                    var inputs = card.Controls.Cast<Control>()
                        .Where(x => x is RoundedButton or ToggleSwitch).ToArray();
                    for (int a = 0; a < inputs.Length; a++)
                    {
                        if (inputs[a].Right > card.ClientSize.Width - 3 ||
                            inputs[a].Bottom > card.ClientSize.Height - 3)
                            throw new InvalidOperationException("Clipped input control: " + inputs[a].Text);
                        for (int b = a + 1; b < inputs.Length; b++)
                            if (inputs[a].Bounds.IntersectsWith(inputs[b].Bounds))
                                throw new InvalidOperationException("Overlapping inputs: "
                                    + inputs[a].Text + " / " + inputs[b].Text);
                    }
                }
                for (int i = 0; i < cards.Length; i++)
                {
                    for (int j = i + 1; j < cards.Length; j++)
                        if (cards[i].Bounds.IntersectsWith(cards[j].Bounds))
                            throw new InvalidOperationException("Overlapping cards: " +
                                cards[i].Bounds + " / " + cards[j].Bounds);
                }
                if (cards.Max(x => x.Width) > 1328)
                    throw new InvalidOperationException("Dashboard width exceeds safe max-width");

                // This is a visual inspection aid, not a substitute for field checking
                // rendering on the user's actual Windows DPI / display hardware.
                try
                {
                    using var image = new Bitmap(window.Width, window.Height);
                    window.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                    image.Save(Path.Combine(outputDirectory,
                        $"drivesync-layout-{width}x{height}.png"), ImageFormat.Png);
                }
                catch (Exception imageEx)
                {
                    Console.WriteLine("Optional screenshot not available: " + imageEx.GetType().Name);
                }
            }
            Console.WriteLine("LAYOUT_AUDIT_PASS: opaque cards, aligned bounds, no overlaps, no clipped labels.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 4;
        }
    }

    private static IEnumerable<Control> GetDescendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in GetDescendants(child))
                yield return nested;
        }
    }
}
