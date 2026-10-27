using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace SkiaSharp.Views.Desktop
{
	[DefaultEvent("PaintSurface")]
	[DefaultProperty("Name")]
	public class SKControl : Control
	{
		private readonly bool designMode;

		private Bitmap bitmap;

		public SKControl()
		{
			DoubleBuffered = false;
			SetStyle(ControlStyles.ResizeRedraw, true);

            designMode = DesignMode || LicenseManager.UsageMode == LicenseUsageMode.Designtime;
		}

		[Bindable(false)]
		[Browsable(false)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public SKSize CanvasSize => Bitmap == null ? SKSize.Empty : new SKSize(Bitmap.Width, Bitmap.Height);

        public Bitmap Bitmap
        {
            get
            {
                return bitmap;
            }
        }

        [Category("Appearance")]
		public event EventHandler<SKPaintSurfaceEventArgs> PaintSurface;

		protected override void OnPaint(PaintEventArgs e)
		{
			if (designMode)
				return;

			base.OnPaint(e);

			Draw();

			// a bitmap at the screen's scale is drawn back at the control's size in points
			if (Bitmap != null && (Bitmap.Width != Width || Bitmap.Height != Height))
				e.Graphics.DrawImage(Bitmap, new Rectangle(0, 0, Width, Height));
			else
				e.Graphics.DrawImage(Bitmap, 0, 0);
		}

		/// <summary>
		/// Pixels a point in the bitmap: on the Mac, 2 on a Retina screen, so what is painted is as sharp
		/// as the screen
		/// </summary>
		private static float DeviceScale()
		{
#if NETSTANDARD2_0
			return Math.Max(1f, XplatUIMine.DeviceScale);
#else
			return 1f;
#endif
		}

		public void Draw()
        {
			// get the bitmap
			var info = CreateBitmap();

			if (info.Width == 0 || info.Height == 0)
				return;

			var data = Bitmap.LockBits(new Rectangle(0, 0, info.Width, info.Height), ImageLockMode.WriteOnly, Bitmap.PixelFormat);

			// create the surface
			using (var surface = SKSurface.Create(info, data.Scan0, data.Stride))
			{
				// drawn in points whatever the bitmap's scale
				var scale = (float) info.Width / Math.Max(1, Width);
				if (scale != 1)
					surface.Canvas.Scale(scale, scale);

				// start drawing
				OnPaintSurface(new SKPaintSurfaceEventArgs(surface, info));

				surface.Canvas.Flush();
			}

			// write the bitmap to the graphics
			Bitmap.UnlockBits(data);
		}

		protected virtual void OnPaintSurface(SKPaintSurfaceEventArgs e)
		{
			// invoke the event
			PaintSurface?.Invoke(this, e);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			FreeBitmap();
		}

		private SKImageInfo CreateBitmap()
		{
			var scale = DeviceScale();
			var info = new SKImageInfo((int) Math.Ceiling(Width * scale), (int) Math.Ceiling(Height * scale),
				SKImageInfo.PlatformColorType, SKAlphaType.Premul);

			if (Bitmap == null || Bitmap.Width != info.Width || Bitmap.Height != info.Height)
			{
				FreeBitmap();

				if (info.Width != 0 && info.Height != 0)
					bitmap = new Bitmap(info.Width, info.Height, PixelFormat.Format32bppPArgb);
            }

			return info;
		}

		private void FreeBitmap()
		{
			if (Bitmap != null)
			{
				Bitmap.Dispose();
				bitmap = null;
			}
		}
	}
}
