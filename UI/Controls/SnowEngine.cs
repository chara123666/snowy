using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Snowy.UI.Controls;

public sealed class SnowEngine : IDisposable
{
    private struct Flake
    {
        public float X, Y, BaseX, Speed, Size, Phase, SwayAmp, SwaySpeed;
        public int Tier;
    }

    private readonly record struct Target(Control Host, bool Scrolls);

    private const int MaxFlakes = 120;
    private const float MinStep = 0.0133f;
    private static readonly int[] TierAlpha = { 110, 170, 235 };

    private readonly Flake[] _flakes = new Flake[MaxFlakes];
    private readonly List<Target> _targets = new();
    private readonly SolidBrush?[] _brushes = new SolidBrush?[3];
    private readonly Random _rng = new();
    private readonly Action<float> _onFrame;

    private int _count;
    private Size _bounds = new(800, 600);
    private float _time;
    private float _acc;
    private float _intensity;
    private float _intensityTarget;
    private float _brushIntensity = -1f;
    private bool _subscribed;
    private long _fullRepaintUntil;

    public SnowEngine() => _onFrame = OnFrame;

    public bool Active => _intensityTarget > 0f;

    public void AddTarget(Control host, bool scrolls = false)
    {
        _targets.Add(new Target(host, scrolls));
        host.Paint += (_, e) =>
        {
            if (scrolls && host is ScrollableControl sc)
            {
                var state = e.Graphics.Save();
                var offset = sc.AutoScrollPosition;
                e.Graphics.TranslateTransform(-offset.X, -offset.Y);
                Draw(e.Graphics, Rectangle.Empty);
                e.Graphics.Restore(state);
            }
            else Draw(e.Graphics, e.ClipRectangle);
        };
    }

    public void SetBounds(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0 || size == _bounds) return;
        _bounds = size;
        if (_count == 0) return;

        _fullRepaintUntil = Environment.TickCount64 + 250;

        for (int i = 0; i < _count; i++)
        {
            ref var f = ref _flakes[i];
            if (f.BaseX > size.Width || f.Y > size.Height) Respawn(ref f);
        }

        int desired = DesiredCount();
        while (_count < desired) { Seed(ref _flakes[_count]); _count++; }
        if (_count > desired) _count = desired;

        foreach (var t in _targets) t.Host.Invalidate();
    }

    public void SetActive(bool active)
    {
        _intensityTarget = active ? 1f : 0f;
        if (!active && _intensity <= 0f) return;

        if (active && _count == 0) SeedAll();
        if (!_subscribed)
        {
            _subscribed = true;
            FrameClock.Subscribe(_onFrame);
        }
    }

    private void OnFrame(float dt)
    {
        _acc += dt;
        if (_acc < MinStep) return;
        dt = _acc;
        _acc = 0f;

        float diff = _intensityTarget - _intensity;
        float fade = (diff > 0f ? 1.3f : 2.0f) * dt;
        _intensity = Math.Abs(diff) <= fade ? _intensityTarget : _intensity + Math.Sign(diff) * fade;
        if (_intensity != _brushIntensity) RebuildBrushes();

        Control? host = PickVisibleTarget(out bool scrolls);
        if (host == null)
        {
            if (_intensity <= 0f && _intensityTarget <= 0f) Stop();
            return;
        }

        bool fullRepaint = scrolls || Environment.TickCount64 < _fullRepaintUntil;

        _time += dt;
        for (int i = 0; i < _count; i++)
        {
            ref var f = ref _flakes[i];
            Rectangle before = fullRepaint ? Rectangle.Empty : RectOf(in f);

            f.Y += f.Speed * dt;
            f.X = f.BaseX + MathF.Sin(_time * f.SwaySpeed + f.Phase) * f.SwayAmp;

            bool wrapped = f.Y > _bounds.Height + 4;
            if (wrapped) Respawn(ref f);
            if (fullRepaint) continue;

            Rectangle after = RectOf(in f);
            if (wrapped) { host.Invalidate(before); host.Invalidate(after); }
            else host.Invalidate(Rectangle.Union(before, after));
        }
        if (fullRepaint) host.Invalidate();

        if (_intensity <= 0f && _intensityTarget <= 0f) Stop();
    }

    private Control? PickVisibleTarget(out bool scrolls)
    {
        foreach (var t in _targets)
        {
            if (t.Host.Visible && t.Host.TopLevelControl is Form { WindowState: not FormWindowState.Minimized })
            {
                scrolls = t.Scrolls;
                return t.Host;
            }
        }
        scrolls = false;
        return null;
    }

    private void Stop()
    {
        if (_subscribed)
        {
            _subscribed = false;
            FrameClock.Unsubscribe(_onFrame);
        }
        _count = 0;
        _acc = 0f;
    }

    public void Draw(Graphics g, Rectangle clip)
    {
        if (_count == 0 || _intensity <= 0.002f) return;

        var oldMode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < _count; i++)
        {
            ref var f = ref _flakes[i];
            if (!clip.IsEmpty && !clip.IntersectsWith(RectOf(in f))) continue;
            g.FillEllipse(_brushes[f.Tier]!, f.X, f.Y, f.Size, f.Size);
        }
        g.SmoothingMode = oldMode;
    }

    private static Rectangle RectOf(in Flake f)
        => new((int)f.X - 3, (int)f.Y - 3, (int)f.Size + 7, (int)f.Size + 7);

    private int DesiredCount() => Math.Clamp(_bounds.Width * _bounds.Height / 11000, 28, MaxFlakes);

    private void SeedAll()
    {
        _count = DesiredCount();
        for (int i = 0; i < _count; i++) Seed(ref _flakes[i]);
    }

    private void Seed(ref Flake f)
    {
        float size = 1.5f + _rng.NextSingle() * 3f;
        f.Size = size;
        f.Speed = 26f + size * 15f + _rng.NextSingle() * 16f;
        f.Tier = size < 2.4f ? 0 : size < 3.5f ? 1 : 2;
        f.BaseX = _rng.NextSingle() * _bounds.Width;
        f.Y = _rng.NextSingle() * _bounds.Height;
        f.Phase = _rng.NextSingle() * 6.2832f;
        f.SwayAmp = 5f + _rng.NextSingle() * 13f;
        f.SwaySpeed = 0.6f + _rng.NextSingle() * 1.1f;
        f.X = f.BaseX;
    }

    private void Respawn(ref Flake f)
    {
        f.Y = -f.Size - _rng.Next(0, 30);
        f.BaseX = _rng.NextSingle() * _bounds.Width;
        f.X = f.BaseX;
    }

    private void RebuildBrushes()
    {
        _brushIntensity = _intensity;
        for (int i = 0; i < _brushes.Length; i++)
        {
            _brushes[i]?.Dispose();
            _brushes[i] = new SolidBrush(Color.FromArgb((int)(TierAlpha[i] * _intensity), 255, 255, 255));
        }
    }

    public void Dispose()
    {
        Stop();
        for (int i = 0; i < _brushes.Length; i++)
        {
            _brushes[i]?.Dispose();
            _brushes[i] = null;
        }
    }
}