using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace MissionPlanner
{
    public partial class Splash : Form
    {
        // the app's accent green, as used by the icon
        private static readonly Color Accent = Color.FromArgb(138, 190, 62);

        private bool customLogo;

        public Splash()
        {
            InitializeComponent();

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            // a rebranded build supplies its own logo.png, show that in place of ours
            if (Program.Logo != null)
            {
                customLogo = true;
                pictureBox1.Location = new Point((ClientSize.Width - pictureBox1.Width) / 2,
                    (ClientSize.Height - pictureBox1.Height) / 2);
                pictureBox1.BackgroundImage = MissionPlanner.Properties.Resources.bgdark;
                pictureBox1.Image = Program.Logo;
                pictureBox1.Visible = true;
            }

            Console.WriteLine("Splash .ctor");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // a rebranded build supplies its own splashbg.png
            if (BackgroundImage != null)
            {
                base.OnPaint(e);
                return;
            }

            var g = e.Graphics;
            var rect = ClientRectangle;

            using (var brush = new LinearGradientBrush(rect, Color.FromArgb(44, 44, 42), Color.FromArgb(20, 20, 19),
                LinearGradientMode.Vertical))
                g.FillRectangle(brush, rect);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var centre = new PointF(rect.Width / 2f, rect.Height / 2f - 10);

            if (!customLogo)
            {
                // a soft glow behind the logo, built from a few faint rings
                for (var i = 9; i >= 1; i--)
                {
                    var r = 40f + i * 17f;
                    using (var brush = new SolidBrush(Color.FromArgb(7, Accent)))
                        g.FillEllipse(brush, centre.X - r, centre.Y - r, r * 2, r * 2);
                }

                var logo = MissionPlanner.Properties.Resources.splashlogo;
                const int size = 200;
                g.DrawImage(logo, new RectangleF(centre.X - size / 2f, centre.Y - size / 2f, size, size));
            }

            // a thin accent line along the bottom edge, fading out at both ends
            var half = rect.Width / 2;
            var left = new Rectangle(0, rect.Height - 3, half + 1, 3);
            var right = new Rectangle(half, rect.Height - 3, rect.Width - half, 3);
            using (var brush = new LinearGradientBrush(left, Color.FromArgb(0, Accent), Accent, LinearGradientMode.Horizontal))
                g.FillRectangle(brush, left);
            using (var brush = new LinearGradientBrush(right, Accent, Color.FromArgb(0, Accent), LinearGradientMode.Horizontal))
                g.FillRectangle(brush, right);

            using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255)))
                g.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
        }

        protected override void OnBackgroundImageChanged(EventArgs e)
        {
            // Mono cannot draw part of a 24 bit image, which the transparent labels need,
            // so they show as blank boxes. A 32 bit copy draws everywhere.
            if (BackgroundImage is Bitmap bitmap && bitmap.PixelFormat != PixelFormat.Format32bppArgb)
            {
                BackgroundImage = new Bitmap(bitmap);
                return;
            }

            base.OnBackgroundImageChanged(e);
        }
    }
}
