using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Snowy.UI;

public static class FrameClock
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    private const double MinFrameGap = 0.004;

    private static readonly object Gate = new();
    private static readonly List<Action<float>> Subscribers = new();
    private static readonly List<Action<float>> Batch = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly Action FrameCallback = RunFrame;

    private static Control? _sink;
    private static System.Windows.Forms.Timer? _watchdog;
    private static Thread? _vsyncThread;
    private static int _framePending;
    private static double _lastFrameAt;
    private static bool _inFrame;

    public static void Subscribe(Action<float> onFrame)
    {
        EnsureInfrastructure();
        lock (Gate)
        {
            if (Subscribers.Contains(onFrame)) return;
            Subscribers.Add(onFrame);

            if (Subscribers.Count == 1)
            {
                _lastFrameAt = Clock.Elapsed.TotalSeconds;
                _watchdog!.Start();
            }
            if (_vsyncThread == null)
            {
                _vsyncThread = new Thread(VsyncLoop) { IsBackground = true, Name = "snowy-vsync" };
                _vsyncThread.Start();
            }
        }
    }

    public static void Unsubscribe(Action<float> onFrame)
    {
        lock (Gate)
        {
            Subscribers.Remove(onFrame);
            if (Subscribers.Count == 0) _watchdog?.Stop();
        }
    }

    private static void EnsureInfrastructure()
    {
        if (_sink != null) return;

        _sink = new Control();
        _ = _sink.Handle;

        _watchdog = new System.Windows.Forms.Timer { Interval = 40 };
        _watchdog.Tick += (_, _) =>
        {
            if (Clock.Elapsed.TotalSeconds - _lastFrameAt > 0.05) RunFrame();
        };
    }

    private static void VsyncLoop()
    {
        double lastPost = 0;
        while (true)
        {
            lock (Gate)
            {
                if (Subscribers.Count == 0) { _vsyncThread = null; return; }
            }

            long t0 = Stopwatch.GetTimestamp();
            int hr;
            try { hr = DwmFlush(); }
            catch { hr = -1; }
            double waited = (Stopwatch.GetTimestamp() - t0) / (double)Stopwatch.Frequency;

            if (hr != 0 || waited < 0.001) Thread.Sleep(16);

            double gap = Clock.Elapsed.TotalSeconds - lastPost;
            if (gap < MinFrameGap)
                Thread.Sleep(Math.Max(1, (int)Math.Ceiling((MinFrameGap - gap) * 1000)));

            if (Interlocked.Exchange(ref _framePending, 1) == 0)
            {
                lastPost = Clock.Elapsed.TotalSeconds;
                try { _sink!.BeginInvoke(FrameCallback); }
                catch { Volatile.Write(ref _framePending, 0); }
            }
        }
    }

    private static void RunFrame()
    {
        Volatile.Write(ref _framePending, 0);
        if (_inFrame) return;
        _inFrame = true;
        try
        {
            double now = Clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(now - _lastFrameAt, 0.1);
            _lastFrameAt = now;

            Batch.Clear();
            lock (Gate) Batch.AddRange(Subscribers);

            for (int i = 0; i < Batch.Count; i++)
            {
                try { Batch[i](dt); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }
        }
        finally { _inFrame = false; }
    }
}