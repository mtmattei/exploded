using System;
using Exploded.Catalog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace Exploded.Presentation;

/// <summary>
/// Shows a money amount on a TextBlock and counts to each new value instead of
/// swapping it, so adding a part reads as the total growing by that part.
///
/// An attached property rather than a control because TextBlock is sealed, and
/// rather than a binding on Text because the tween has to write Text locally,
/// which would replace that binding.
///
/// House motion values: DurationSlow (280 ms) on EaseSmooth (0.22,1 0.36,1).
/// With animations turned off in the OS the amount is set at once.
/// </summary>
public static class CountUp
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(280);
    private static readonly UISettings Settings = new();

    public static readonly DependencyProperty AmountProperty =
        DependencyProperty.RegisterAttached(
            "Amount",
            typeof(object),
            typeof(CountUp),
            new PropertyMetadata(null, OnAmountChanged));

    // Per-TextBlock tween state: what is on screen, and the run in flight.
    private static readonly DependencyProperty TweenProperty =
        DependencyProperty.RegisterAttached("Tween", typeof(Tween), typeof(CountUp), new PropertyMetadata(null));

    public static object? GetAmount(TextBlock element) => element.GetValue(AmountProperty);

    public static void SetAmount(TextBlock element, object? value) => element.SetValue(AmountProperty, value);

    private static void OnAmountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text || e.NewValue is null)
        {
            return;
        }

        var target = Convert.ToDecimal(e.NewValue);

        if (text.GetValue(TweenProperty) is not Tween tween)
        {
            // First value: nothing to count from.
            tween = new Tween(text, target);
            text.SetValue(TweenProperty, tween);
            return;
        }

        tween.RunTo(target, animate: Settings.AnimationsEnabled);
    }

    private sealed class Tween
    {
        private readonly TextBlock _text;
        private decimal _shown;
        private decimal _from;
        private decimal _to;
        private DateTime _start;
        private bool _running;

        public Tween(TextBlock text, decimal amount)
        {
            _text = text;
            Show(amount);
        }

        public void RunTo(decimal target, bool animate)
        {
            if (!animate)
            {
                Stop();
                Show(target);
                return;
            }

            // Retargeting mid-run starts from what is on screen, so the count
            // never jumps backwards.
            _from = _shown;
            _to = target;
            _start = DateTime.UtcNow;

            if (!_running)
            {
                _running = true;
                CompositionTarget.Rendering += OnFrame;
            }
        }

        private void OnFrame(object? sender, object e)
        {
            var progress = Math.Min(1d, (DateTime.UtcNow - _start) / Duration);
            var eased = (decimal)EaseSmooth(progress);

            Show(progress >= 1d ? _to : Math.Round(_from + (_to - _from) * eased, 2));

            if (progress >= 1d)
            {
                Stop();
            }
        }

        private void Stop()
        {
            if (_running)
            {
                _running = false;
                CompositionTarget.Rendering -= OnFrame;
            }
        }

        private void Show(decimal amount)
        {
            _shown = amount;
            _text.Text = Part.FormatPrice(amount);
        }
    }

    /// <summary>
    /// cubic-bezier(0.22, 1, 0.36, 1): solve x(t) = progress for t by Newton's
    /// method, then return y(t). The curve is monotonic in x, so a few steps
    /// from t = progress converge.
    /// </summary>
    private static double EaseSmooth(double progress)
    {
        const double x1 = 0.22, y1 = 1d, x2 = 0.36, y2 = 1d;

        static double Bezier(double t, double p1, double p2)
            => 3 * (1 - t) * (1 - t) * t * p1 + 3 * (1 - t) * t * t * p2 + t * t * t;

        static double Slope(double t, double p1, double p2)
            => 3 * (1 - t) * (1 - t) * p1 + 6 * (1 - t) * t * (p2 - p1) + 3 * t * t * (1 - p2);

        var t = progress;

        for (var i = 0; i < 6; i++)
        {
            var slope = Slope(t, x1, x2);

            if (Math.Abs(slope) < 1e-6)
            {
                break;
            }

            t = Math.Clamp(t - (Bezier(t, x1, x2) - progress) / slope, 0d, 1d);
        }

        return Bezier(t, y1, y2);
    }
}
