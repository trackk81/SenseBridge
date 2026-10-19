using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;
namespace SenseBridge {
static class UiShape {
 public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}
public sealed class RoundedButton:Button {
 public bool NavigationStyle{get;set;}
 public bool SelectedStyle{get;set;}
 bool hover,pressed;
 public RoundedButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent==null?BackColor:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float s=g.DpiX/96f;var box=new RectangleF(s,s,Width-2*s,Height-2*s);bool accent=BackColor.B>BackColor.R+50;Color fill=accent?(pressed?Color.FromArgb(97,80,180):hover?Color.FromArgb(139,119,235):Color.FromArgb(119,99,213)):(pressed?Color.FromArgb(43,47,63):hover?Color.FromArgb(40,45,61):Color.FromArgb(27,31,43));
 if(NavigationStyle){fill=SelectedStyle?Color.FromArgb(68,72,87):hover||Focused?Color.FromArgb(48,53,67):Parent.BackColor;}
 using(var path=UiShape.Round(box,NavigationStyle?22*s:12*s)){using(var brush=new SolidBrush(fill))g.FillPath(brush,path);if(!NavigationStyle)using(var pen=new Pen(Focused?Color.FromArgb(172,157,243):accent?fill:Color.FromArgb(53,59,76),s))g.DrawPath(pen,path);}
 TextRenderer.DrawText(g,Text,Font,Rectangle.Round(box),Enabled?ForeColor:Color.FromArgb(116,123,141),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
 }
}
public sealed class NavigationBar:Panel {
 public NavigationBar(){DoubleBuffered=true;BackColor=Color.FromArgb(29,33,43);SetStyle(ControlStyles.ResizeRedraw,true);}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=e.Graphics.DpiX/96f;using(var p=UiShape.Round(new RectangleF(s,s,Width-2*s,Height-2*s),30*s)){using(var b=new SolidBrush(BackColor))e.Graphics.FillPath(b,p);using(var pen=new Pen(Color.FromArgb(67,72,85),s))e.Graphics.DrawPath(pen,p);}}
}
public sealed class NavigationColors:ProfessionalColorTable {
 public override Color ToolStripDropDownBackground{get{return Color.FromArgb(29,33,43);}}
 public override Color MenuItemSelected{get{return Color.FromArgb(57,52,82);}}
 public override Color MenuItemBorder{get{return Color.FromArgb(90,79,129);}}
 public override Color MenuBorder{get{return Color.FromArgb(67,72,85);}}
 public override Color ImageMarginGradientBegin{get{return ToolStripDropDownBackground;}}
 public override Color ImageMarginGradientMiddle{get{return ToolStripDropDownBackground;}}
 public override Color ImageMarginGradientEnd{get{return ToolStripDropDownBackground;}}
}
}
