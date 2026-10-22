using System.Runtime.InteropServices;
using System.Text;

namespace Snowy.UI;

public enum Sound { Ui, Theme }

public static class SoundFx
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, StringBuilder? returnValue, int returnLength, IntPtr callback);

    private static readonly Dictionary<Sound, string> Files = new()
    {
        [Sound.Ui] = "ui.mp3",
        [Sound.Theme] = "theme.mp3",
    };

    private static readonly HashSet<Sound> Opened = new();
    private static readonly HashSet<Sound> Failed = new();

    private static string Alias(Sound sound) => $"snowy_sfx_{sound}";

    public static void Play(Sound sound = Sound.Ui)
    {
        if (Failed.Contains(sound)) return;
        try
        {
            if (!Opened.Contains(sound) && !Open(sound)) { Failed.Add(sound); return; }
            mciSendString($"seek {Alias(sound)} to start", null, 0, IntPtr.Zero);
            mciSendString($"play {Alias(sound)}", null, 0, IntPtr.Zero);
        }
        catch
        {
            Failed.Add(sound);
        }
    }

    private static bool Open(Sound sound)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Sounds", Files[sound]);
        if (!File.Exists(path)) return false;

        if (mciSendString($"open \"{path}\" type mpegvideo alias {Alias(sound)}", null, 0, IntPtr.Zero) != 0)
            return false;

        Opened.Add(sound);
        return true;
    }

    public static void Shutdown()
    {
        foreach (var sound in Opened)
            mciSendString($"close {Alias(sound)}", null, 0, IntPtr.Zero);
        Opened.Clear();
    }
}