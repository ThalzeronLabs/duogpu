using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DuoGPU.UI;

/// <summary>The safety net, like the one Windows shows after a resolution change: if nobody clicks "Keep" in time
/// (because the screen is black), the switch is undone.</summary>
public sealed class ConfirmWindow : Window
{
    readonly TaskCompletionSource<bool> done = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly DateTime end;
    readonly TextBlock countdown;

    ConfirmWindow(string gdiName, int seconds)
    {
        end = DateTime.Now.AddSeconds(seconds);
        Title = "DuoGPU";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        ResizeMode = ResizeMode.NoResize;
        Left = -10000;

        var content = BuildContent(() => Finish(true), seconds);
        countdown = (TextBlock)content.Tag;
        Content = content;

        ContentRendered += (_, _) =>
        {
            var b = Ui.Bounds(gdiName);
            Ui.PlaceInPixels(this, (w, h) => (b.X + (b.Width - w) / 2, b.Y + (b.Height - h) / 2));
            Activate();
        };
        timer.Tick += (_, _) =>
        {
            int left = (int)Math.Ceiling((end - DateTime.Now).TotalSeconds);
            if (left <= 0) Finish(false);
            else countdown.Text = S.ConfirmCountdown(left);
        };
        Closed += (_, _) => done.TrySetResult(false);
    }

    /// <summary>The window's content. The countdown text is returned in <c>Tag</c>.</summary>
    public static FrameworkElement BuildContent(Action? onKeep, int seconds)
    {
        var question = Ui.Label(S.ConfirmQuestion, 28, Ui.Text, FontWeights.SemiBold);
        question.TextAlignment = TextAlignment.Center;
        var countdown = Ui.Label(S.ConfirmCountdown(seconds), 16, Ui.Muted);
        countdown.TextAlignment = TextAlignment.Center;
        countdown.Margin = new Thickness(0, 10, 0, 0);
        var keep = Ui.Button(S.Keep, Ui.Green, onKeep ?? (() => { }), 20);
        keep.Margin = new Thickness(0, 24, 0, 0);

        var stack = new StackPanel { Width = 520 };
        stack.Children.Add(question);
        stack.Children.Add(countdown);
        stack.Children.Add(keep);
        return new Border
        {
            Background = Ui.Background,
            BorderBrush = Ui.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(40, 34, 40, 34),
            Child = stack,
            Tag = countdown,
        };
    }

    void Finish(bool kept)
    {
        timer.Stop();
        done.TrySetResult(kept);
        Close();
    }

    /// <summary>Shows the window on the given monitor and waits for "Keep" or the timeout.</summary>
    public static Task<bool> AskAsync(string gdiName, int seconds)
    {
        var w = new ConfirmWindow(gdiName, seconds);
        w.Show();
        w.timer.Start();
        return w.done.Task;
    }
}
