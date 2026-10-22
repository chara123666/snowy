using System.Windows.Forms;

namespace Snowy.UI.Controls;

public class BufferedPanel : Panel
{
    public BufferedPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;
    }
}