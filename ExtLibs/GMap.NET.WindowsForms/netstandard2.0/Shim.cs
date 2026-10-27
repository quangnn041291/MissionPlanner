using SkiaSharp;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;


namespace SvgNet.SvgGdi
{
}
namespace OpenTK.Graphics{}
namespace OpenTK.Platform{}
public class GdiGraphics : Graphics
{
    public GdiGraphics(SKSurface surface) : base(surface)
    {
    }

    public GdiGraphics(IntPtr handle, int width, int height) : base(handle, width, height)
    {
    }

    // draw to the same canvas, which for a window is not its Surface
    public GdiGraphics(Graphics fromImage) : base(fromImage)
    {
    }
}

public class SkiaGraphics : Graphics
{
    public SkiaGraphics(SKSurface surface) : base(surface)
    {
    }

    public SkiaGraphics(IntPtr handle, int width, int height) : base(handle, width, height)
    {
    }

    public SKCanvas Image => _image;
}
