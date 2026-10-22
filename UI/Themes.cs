using System.Drawing;
using System.Windows.Forms;

namespace Snowy.UI;

public enum Slot
{
    Background, Panel, PanelAlt,
    Accent, AccentDim, AccentGlow,
    TextPrimary, TextDim,
    Success, Danger,
    GradientTop, GradientBottom
}

public sealed class Palette
{
    public const int SlotCount = (int)Slot.GradientBottom + 1;

    public const int BindableSlotCount = (int)Slot.GradientTop;

    private readonly Color[] _colors;

    public string Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }

    public Palette(string id, string nameKey, string descKey, Color[] colors)
    {
        if (colors.Length != SlotCount)
            throw new ArgumentException($"palette needs exactly {SlotCount} colors", nameof(colors));
        Id = id;
        NameKey = nameKey;
        DescKey = descKey;
        _colors = colors;
    }

    public Color this[Slot slot] => _colors[(int)slot];
    public Color this[int index] => _colors[index];

    public int SlotOf(Color c)
    {
        int argb = c.ToArgb();
        for (int i = 0; i < BindableSlotCount; i++)
            if (_colors[i].ToArgb() == argb) return i;
        return -1;
    }

    internal Palette Clone(string id) => new(id, NameKey, DescKey, (Color[])_colors.Clone());

    internal void CopyTo(Color[] dest) => Array.Copy(_colors, dest, SlotCount);

    internal void SetBlend(Color[] from, Palette to, float t)
    {
        for (int i = 0; i < SlotCount; i++)
            _colors[i] = Theme.Lerp(from[i], to._colors[i], t);
    }
}

public static class Palettes
{
    private static Color H(string hex) => ColorTranslator.FromHtml(hex);

    public static readonly Palette Snowy = new("snowy", "theme.snowy.name", "theme.snowy.desc", new[]
    {
        H("#0A0A0F"), H("#13131C"), H("#1E1E2E"),
        H("#EDEDED"), H("#5A5A66"), H("#FFFFFF"),
        H("#E0E0E0"), H("#55556A"),
        H("#4CD787"), H("#FF4444"),
        H("#000000"), H("#2E2E36")
    });

    public static readonly Palette Fire = new("fire", "theme.fire.name", "theme.fire.desc", new[]
    {
        H("#0F0708"), H("#1C0F0E"), H("#2E1A17"),
        H("#FF7A3D"), H("#7A3B2B"), H("#FFA477"),
        H("#F2DCD3"), H("#7A5B54"),
        H("#6FD48A"), H("#FF4D5E"),
        H("#0A0000"), H("#3A1208")
    });

    public static readonly Palette Nature = new("nature", "theme.nature.name", "theme.nature.desc", new[]
    {
        H("#060D09"), H("#0E1A13"), H("#16281D"),
        H("#7EE2A0"), H("#3F6B4E"), H("#B8FFD0"),
        H("#DCEFE2"), H("#55705F"),
        H("#B5E655"), H("#FF5C5C"),
        H("#000A04"), H("#123322")
    });

    public static readonly IReadOnlyList<Palette> All = new[] { Snowy, Fire, Nature };
}

public static class ThemeManager
{
    private sealed class Binding
    {
        public readonly Control Control;
        public readonly int Back;
        public readonly int Fore;
        public Binding(Control control, int back, int fore) { Control = control; Back = back; Fore = fore; }
    }

    private static readonly List<Binding> Bindings = new();
    private static readonly List<Control> Repaints = new();
    private static readonly HashSet<Control> Seen = new();
    private static readonly Color[] From = new Color[Palette.SlotCount];
    private static readonly Palette Mix = Palettes.Snowy.Clone("mix");

    private static Palette _settled = Palettes.Snowy;
    private static Action? _cancel;
    private static int _generation;

    public static Palette Target { get; private set; } = Palettes.Snowy;

    public static void SwitchTo(Palette target, params Control[] roots)
    {
        if (ReferenceEquals(target, Target)) return;
        Target = target;
        int gen = ++_generation;
        _cancel?.Invoke();

        Theme.Current.CopyTo(From);
        Refresh(roots);

        _cancel = Animator.Run(240, t =>
        {
            if (gen != _generation) return;
            if (t >= 1f) { Finish(target); return; }

            Mix.SetBlend(From, target, t);
            Theme.SetCurrent(Mix);
            Apply(Mix, includeHidden: false);
        });
    }

    private static void Finish(Palette target)
    {
        Theme.SetCurrent(target);
        _settled = target;
        _cancel = null;
        Apply(target, includeHidden: true);
    }

    private static void Refresh(Control[] roots)
    {
        Bindings.RemoveAll(b => b.Control.IsDisposed);
        Repaints.RemoveAll(c => c.IsDisposed);
        Seen.RemoveWhere(c => c.IsDisposed);
        foreach (var root in roots) Collect(root);
    }

    private static void Collect(Control c)
    {
        if (Seen.Add(c))
        {
            int back = _settled.SlotOf(c.BackColor);
            int fore = _settled.SlotOf(c.ForeColor);
            if (back >= 0 || fore >= 0) Bindings.Add(new Binding(c, back, fore));

            if (c is not Form && c.GetType().Namespace is { } ns && ns.StartsWith("Snowy.UI", StringComparison.Ordinal))
                Repaints.Add(c);
        }
        foreach (Control child in c.Controls) Collect(child);
    }

    private static bool OnScreen(Control c) => c.Visible && c.TopLevelControl != null;

    private static void Apply(Palette pal, bool includeHidden)
    {
        for (int i = 0; i < Bindings.Count; i++)
        {
            var b = Bindings[i];
            var c = b.Control;
            if (c.IsDisposed) continue;
            if (!includeHidden && !OnScreen(c)) continue;
            if (b.Back >= 0) c.BackColor = pal[b.Back];
            if (b.Fore >= 0) c.ForeColor = pal[b.Fore];
        }

        for (int i = 0; i < Repaints.Count; i++)
        {
            var c = Repaints[i];
            if (!c.IsDisposed && (includeHidden || OnScreen(c))) c.Invalidate();
        }
    }
}