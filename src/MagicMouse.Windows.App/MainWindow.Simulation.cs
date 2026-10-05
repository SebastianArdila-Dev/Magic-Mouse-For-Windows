using MagicMouse.Core.Gestures;
using MagicMouse.Core.Touch;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private Canvas? _simulationContacts;
    private bool _simulationRunning;

    private async Task SimulateGestureAsync(string action)
    {
        if (_simulationRunning) return;
        _simulationRunning = true;
        var contacts = _simulationContacts;
        var recognizer = new GestureSequenceRecognizer(new(
            MinimumSwipeDistance: Preferences.Numbers.GetValueOrDefault("gesture.distance", 12) / 100,
            MaximumTapMovement: Preferences.Numbers.GetValueOrDefault("tap.movement", 3.5) / 100,
            MaximumSwipeDuration: TimeSpan.FromMilliseconds(Preferences.Numbers.GetValueOrDefault("gesture.timeout", 900)),
            MaximumTapDuration: TimeSpan.FromMilliseconds(Preferences.Numbers.GetValueOrDefault("tap.timeout", 320))));
        var started = DateTimeOffset.UtcNow;
        var fingers = action.Contains("3 dedos") ? 3 : action.Contains("2 dedos") ? 2 : 1;
        var scroll = new ScrollEngine(new(
            Enabled: Preferences.Toggles.GetValueOrDefault("scroll.enabled", true),
            Vertical: Preferences.Toggles.GetValueOrDefault("scroll.vertical", true),
            Horizontal: Preferences.Toggles.GetValueOrDefault("scroll.horizontal", true),
            Natural: Preferences.Toggles.GetValueOrDefault("scroll.natural"),
            InvertVertical: Preferences.Toggles.GetValueOrDefault("scroll.invertVertical"),
            InvertHorizontal: Preferences.Toggles.GetValueOrDefault("scroll.invertHorizontal"),
            AxisLock: Preferences.Toggles.GetValueOrDefault("scroll.axisLock", true),
            Smooth: Preferences.Toggles.GetValueOrDefault("scroll.smooth", true),
            Speed: Preferences.Numbers.GetValueOrDefault("scroll.speed", 8) / 8,
            Sensitivity: Preferences.Numbers.GetValueOrDefault("scroll.sensitivity", 10) / 10));
        var scrollTotal = 0.0;
        try
        {
            for (var i = 0; i <= 8; i++)
            {
                var progress = i / 8.0;
                var x = action.Contains('←') ? .75 - .5 * progress : action.Contains('→') ? .25 + .5 * progress : .5;
                var y = action.Contains('↑') ? .75 - .5 * progress : action.Contains('↓') || action == "Scroll" ? .25 + .5 * progress : .5;
                var frameContacts = Enumerable.Range(1, fingers).Select(id => new TouchContact(id, Math.Clamp(x + (id - (fingers + 1) / 2.0) * .12, 0, 1), y)).ToArray();
                var frame = new TouchFrame(started.AddMilliseconds(i * 18), frameContacts);
                recognizer.Process(frame);
                if (action == "Scroll") scrollTotal += scroll.Process(0, i == 0 ? 0 : .5 / 8, TimeSpan.FromMilliseconds(18), true).Vertical;
                if (contacts is not null)
                {
                    contacts.Children.Clear();
                    foreach (var contact in frameContacts)
                    {
                        var dot = new Ellipse { Width = 24, Height = 24, Fill = new SolidColorBrush(ColorFromHex("#991683FF")), Stroke = new SolidColorBrush(ColorFromHex(DesignTokens.Accent)), StrokeThickness = 2 };
                        var centerX = contacts.Width / 2; var centerY = contacts.Height / 2;
                        Canvas.SetLeft(dot, centerX + (contact.X - .5) * 100 - dot.Width / 2);
                        Canvas.SetTop(dot, centerY + (contact.Y - .5) * 180 - dot.Height / 2); contacts.Children.Add(dot);
                    }
                }
                await Task.Delay(18);
            }
            var events = recognizer.Process(new TouchFrame(started.AddMilliseconds(170), []));
            if (action.Contains("Doble"))
            {
                var tapContacts = Enumerable.Range(1,fingers).Select(id=>new TouchContact(id,.5+(id-(fingers+1)/2.0)*.12,.5)).ToArray();
                recognizer.Process(new(started.AddMilliseconds(300),tapContacts));
                await Task.Delay(130); events = recognizer.Process(new(started.AddMilliseconds(430),[]));
            }
            else if (events.Count == 0) events = recognizer.Flush(started.AddMilliseconds(1000));
            var gesture = events.LastOrDefault();
            var mapped = gesture is null ? "Sin acción reconocida" : GestureActionMapper.Map(gesture, Preferences);
            SetSimulationStatus(action == "Scroll" ? $"DEBUG · scroll acumulado {scrollTotal:0.00}. Respeta dirección, velocidad y sensibilidad guardadas; no envía rueda a Windows." : $"DEBUG · {gesture?.Kind.ToString() ?? action} → {mapped}. Reproducción local; no envía entrada a Windows.");
        }
        finally { contacts?.Children.Clear(); _simulationRunning = false; }
    }
}
