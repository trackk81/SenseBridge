using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;
namespace SenseBridge {
static class UiShape {
 public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}
public sealed class RoundedButton:Button {
 bool hover,pressed;
 public RoundedButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent==null?BackColor:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float s=g.DpiX/96f;var box=new RectangleF(s,s,Width-2*s,Height-2*s);bool accent=BackColor.B>BackColor.R+50;Color fill=accent?(pressed?Color.FromArgb(97,80,180):hover?Color.FromArgb(139,119,235):Color.FromArgb(119,99,213)):(pressed?Color.FromArgb(43,47,63):hover?Color.FromArgb(40,45,61):Color.FromArgb(27,31,43));
 using(var path=UiShape.Round(box,12*s)){using(var brush=new SolidBrush(fill))g.FillPath(brush,path);using(var pen=new Pen(Focused?Color.FromArgb(172,157,243):accent?fill:Color.FromArgb(53,59,76),s))g.DrawPath(pen,path);}
 TextRenderer.DrawText(g,Text,Font,Rectangle.Round(box),Enabled?ForeColor:Color.FromArgb(116,123,141),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
 }
}
}
