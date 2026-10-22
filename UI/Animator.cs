using System.Diagnostics;
using System.Windows.Forms;

namespace Snowy.UI;

public static class Animator
{
    public static Action Run(int durationMs, Action<float> onTick, Action? onComplete = null, bool eased = true)
    {
        var sw = Stopwatch.StartNew();
        double duration = Math.Max(1, durationMs);
        bool done = false;
        Action<float>? frame = null;

        frame = _ =>
        {
            if (done) return;
            float t = (float)Math.Min(sw.Elapsed.TotalMilliseconds / duration, 1.0);
            try { onTick(eased ? 1f - (1f - t) * (1f - t) : t); }
            catch (ObjectDisposedException)
            {
                done = true;
                FrameClock.Unsubscribe(frame!);
                return;
            }
            if (t >= 1f)
            {
                done = true;
                FrameClock.Unsubscribe(frame!);
                onComplete?.Invoke();
            }
        };

        FrameClock.Subscribe(frame);
        return () =>
        {
            if (done) return;
            done = true;
            FrameClock.Unsubscribe(frame!);
        };
    }
}

public sealed class Tween : IDisposable
{
    private readonly Action<float> _onFrame;
    private readonly Action _changed;
    private readonly float _unitsPerSecond;
    private float _target;
    private bool _subscribed;

    public float Value { get; private set; }

    public Tween(float initial, float unitsPerSecond, Action changed)
    {
        Value = _target = initial;
        _unitsPerSecond = unitsPerSecond;
        _changed = changed;
        _onFrame = OnFrame;
    }

    public void To(float target)
    {
        _target = target;
        if (!_subscribed && Value != target)
        {
            _subscribed = true;
            FrameClock.Subscribe(_onFrame);
        }
    }

    private void OnFrame(float dt)
    {
        float step = _unitsPerSecond * dt;
        float diff = _target - Value;
        if (Math.Abs(diff) <= step) { Value = _target; Stop(); }
        else Value += Math.Sign(diff) * step;
        _changed();
    }

    private void Stop()
    {
        if (!_subscribed) return;
        _subscribed = false;
        FrameClock.Unsubscribe(_onFrame);
    }

    public void Dispose() => Stop();
}

public class AnimatedForm : Form
{
    private Action? _cancelFade;

    protected AnimatedForm() => Opacity = 0;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _cancelFade = Animator.Run(160, t => { if (!IsDisposed) Opacity = t; });
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cancelFade?.Invoke();
        base.OnFormClosed(e);
    }
}