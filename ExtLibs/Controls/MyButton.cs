using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;


using System.Drawing.Drawing2D;

namespace MissionPlanner.Controls
{
    public class MyButton : Button
    {
        bool _mouseover = false;
        bool _mousedown = false;

        internal Color _BGGradTop;
        internal Color _BGGradBot;
        internal Color _TextColor;
        internal Color _TextColorNotEnabled;
        internal Color _Outline;
        internal Color _ColorNotEnabled;
        internal Color _ColorMouseOver;
        internal Color _ColorMouseDown;

        bool inOnPaint = false;

        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Colors")]
        [DefaultValue(typeof(Color), "0x94, 0xc1, 0x1f")]
        public Color BGGradTop { get { return _BGGradTop; } set { _BGGradTop = value; this.Invalidate(); } }
        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Colors")]
        [DefaultValue(typeof(Color), "0xcd, 0xe2, 0x96")]
        public Color BGGradBot { get { return _BGGradBot; } set { _BGGradBot = value; this.Invalidate(); } }
        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Colors")]
        [DefaultValue(typeof(Color), "73, 0x2b, 0x3a, 0x03")]
        public Color ColorNotEnabled { get { return _ColorNotEnabled; } set { _ColorNotEnabled = value; this.Invalidate(); } }
        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Colors")]
        [DefaultValue(typeof(Color), "73, 0x2b, 0x3a, 0x03")]
        public Color ColorMouseOver { get { return _ColorMouseOver; } set { _ColorMouseOver = value; this.Invalidate(); } }
        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Colors")]
        [DefaultValue(typeof(Color), "150, 0x2b, 0x3a, 0x03")]
        public Color ColorMouseDown { get { return _ColorMouseDown; } set { _ColorMouseDown = value; this.Invalidate(); } }

        // i want to ignore forecolor
        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Colors")]
        [DefaultValue(typeof(Color), "0x40, 0x57, 0x04")]
        public Color TextColor { get { return _TextColor; } set { _TextColor = value; this.Invalidate(); } }
        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Colors")]
        public Color TextColorNotEnabled { get { return (_TextColorNotEnabled.IsEmpty) ? _TextColor : _TextColorNotEnabled; } set { _TextColorNotEnabled = value; this.Invalidate(); } }
        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Colors")]
        [DefaultValue(typeof(Color), "0x79, 0x94, 0x29")]
        public Color Outline { get { return _Outline; } set { _Outline = value; this.Invalidate(); } }

        protected override Size DefaultSize => base.DefaultSize;

        public MyButton()
        {
            _BGGradTop = Color.FromArgb(0x94, 0xc1, 0x1f);
            _BGGradBot = Color.FromArgb(0xcd, 0xe2, 0x96);
            _TextColor = Color.FromArgb(0x40, 0x57, 0x04);
            _Outline = Color.FromArgb(0x79, 0x94, 0x29);
            _ColorNotEnabled = Color.FromArgb(73, 0x2b, 0x3a, 0x03);
            _ColorMouseOver = Color.FromArgb(73, 0x2b, 0x3a, 0x03);
            _ColorMouseDown = Color.FromArgb(150, 0x2b, 0x3a, 0x03);
        }

        /// <summary>
        /// Radius of the rounded corners, in pixels
        /// </summary>
        [System.ComponentModel.Browsable(true), System.ComponentModel.Category("Appearance")]
        [DefaultValue(4)]
        public int CornerRadius { get; set; } = 4;

        static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();

            float dia = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));

            if (dia < 1)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.Left, rect.Top, dia, dia, 180, 90);
            path.AddArc(rect.Right - dia, rect.Top, dia, dia, 270, 90);
            path.AddArc(rect.Right - dia, rect.Bottom - dia, dia, dia, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - dia, dia, dia, 90, 90);
            path.CloseFigure();

            return path;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            //base.OnPaint(pevent);

            if (inOnPaint)
                return;

            inOnPaint = true;

            try
            {
                Graphics gr = pevent.Graphics;

                // the corners outside the rounded edge show what the button sits on
                Color corner = Parent != null && Parent.BackColor.A == 255 ? Parent.BackColor : this.BackColor;
                gr.Clear(corner);

                gr.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle outside = new Rectangle(0, 0, this.Width, this.Height);

                // fill whole pixels, so no edge is left half covered and showing dark against what is behind
                using (GraphicsPath outline = RoundedRect(new RectangleF(0, 0, this.Width, this.Height), CornerRadius))
                {
                    // flat when the theme gives one colour, a gradient when it gives two
                    if (BGGradTop == BGGradBot)
                    {
                        using (SolidBrush fill = new SolidBrush(BGGradTop))
                            gr.FillPath(fill, outline);
                    }
                    else
                    {
                        using (LinearGradientBrush linear = new LinearGradientBrush(outside, BGGradTop, BGGradBot, LinearGradientMode.Vertical))
                            gr.FillPath(linear, outline);
                    }

                    if (!this.Enabled)
                    {
                        using (SolidBrush brush = new SolidBrush(_ColorNotEnabled))
                            gr.FillPath(brush, outline);
                    }
                    else if (_mousedown)
                    {
                        using (SolidBrush brush = new SolidBrush(ColorMouseDown))
                            gr.FillPath(brush, outline);
                    }
                    else if (_mouseover)
                    {
                        using (SolidBrush brush = new SolidBrush(ColorMouseOver))
                            gr.FillPath(brush, outline);
                    }

                    // an outline the colour of the fill would only blur the edge; a different one is drawn
                    // half a pixel in, so it lands on whole pixels
                    if (Outline.A > 0 && Outline.ToArgb() != BGGradTop.ToArgb())
                    {
                        using (GraphicsPath border = RoundedRect(new RectangleF(0.5f, 0.5f, this.Width - 1, this.Height - 1), CornerRadius))
                        using (Pen mypen = new Pen(Outline, 1))
                            gr.DrawPath(mypen, border);
                    }
                }

                StringFormat stringFormat = new StringFormat();
                stringFormat.Alignment = StringAlignment.Center;
                stringFormat.LineAlignment = StringAlignment.Center;

                string display = this.Text;
                int amppos = display.IndexOf('&');
                if (amppos != -1)
                    display = display.Remove(amppos, 1);

                using (SolidBrush mybrush = new SolidBrush(this.Enabled ? TextColor : TextColorNotEnabled))
                    gr.DrawString(display, this.Font, mybrush, outside, stringFormat);
            }
            catch { }

            inOnPaint = false;
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            //base.OnPaintBackground(pevent);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _mouseover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _mouseover = false;
            _mousedown = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _mousedown = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _mousedown = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }
    }
}
