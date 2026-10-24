using System;using System.Collections.Generic;using System.Collections.ObjectModel;using System.Collections.Specialized;using System.Drawing;using System.Drawing.Drawing2D;using System.IO;using System.Linq;using System.Windows.Forms;using Microsoft.Win32;
namespace SenseBridge {
public sealed class GameGallery:FlowLayoutPanel {
 public ObservableCollection<GameProfile> Items {get;private set;}
 GameProfile selected;public event EventHandler SelectedIndexChanged,ItemActivated;
 public GameProfile SelectedItem {get{return selected;}set{if(selected==value)return;selected=value;RefreshCards();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
 public int SelectedIndex {get{return Items.IndexOf(selected);}set{SelectedItem=value>=0&&value<Items.Count?Items[value]:null;}}
 public GameGallery(){Items=new ObservableCollection<GameProfile>();AutoScroll=true;WrapContents=true;FlowDirection=FlowDirection.LeftToRight;DoubleBuffered=true;BackColor=Color.FromArgb(15,18,27);Padding=new Padding(8);Items.CollectionChanged+=Changed;}
 void Changed(object sender,NotifyCollectionChangedEventArgs e){
  SuspendLayout();try{
   if(e.Action==NotifyCollectionChangedAction.Add){foreach(GameProfile p in e.NewItems){var card=new GameTile(p);card.Click+=delegate{SelectedItem=card.Profile;if(ItemActivated!=null)ItemActivated(this,EventArgs.Empty);};Controls.Add(card);}}
   else {foreach(Control c in Controls.Cast<Control>().ToArray())c.Dispose();Controls.Clear();foreach(var p in Items){var card=new GameTile(p);card.Click+=delegate{SelectedItem=card.Profile;if(ItemActivated!=null)ItemActivated(this,EventArgs.Empty);};Controls.Add(card);}}
   if(selected!=null&&!Items.Contains(selected))SelectedItem=null;
  }finally{ResumeLayout();}RefreshCards();
 }
 public void RefreshCards(){foreach(GameTile c in Controls){c.Selected=c.Profile==selected;c.Invalidate();}}
 protected override void OnInvalidated(InvalidateEventArgs e){base.OnInvalidated(e);RefreshCards();}
}
sealed class GameTile:Control {
 public readonly GameProfile Profile;public bool Selected;Image artwork;bool hover;
 public GameTile(GameProfile profile){Profile=profile;Size=new Size(180,332);Margin=new Padding(12,10,12,14);DoubleBuffered=true;TabStop=true;Cursor=Cursors.Hand;AccessibleName=profile.Name;AccessibleRole=AccessibleRole.PushButton;BackColor=Color.FromArgb(15,18,27);Font=new Font("Malgun Gothic",10);artwork=LoadArtwork(profile);}
 protected override void OnCreateControl(){base.OnCreateControl();using(var g=CreateGraphics()){float s=g.DpiX/96f;Size=new Size((int)(180*s),(int)(332*s));Margin=new Padding((int)(12*s),(int)(10*s),(int)(12*s),(int)(14*s));}}
 static Image ReadImage(string file){try{using(var stream=File.Open(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){using(var img=Image.FromStream(stream)){return new Bitmap(img);}}}catch{return null;}}
 static Image LoadArtwork(GameProfile p){
  if(Store.Supported(p)){string local=Path.Combine(Store.Root,@"assets\bloodborne.png");return File.Exists(local)?ReadImage(local):null;}
  if(string.IsNullOrEmpty(p.SteamAppId))return null;
  try{string steam=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null) as string;if(string.IsNullOrEmpty(steam))return null;
   string cache=Path.Combine(steam,@"appcache\librarycache");
   foreach(string file in new[]{Path.Combine(cache,p.SteamAppId,"library_600x900.jpg"),Path.Combine(cache,p.SteamAppId+"_library_600x900.jpg"),Path.Combine(cache,p.SteamAppId,"library_600x900.png"),Path.Combine(cache,p.SteamAppId,"header.jpg")})if(File.Exists(file)){var image=ReadImage(file);if(image!=null)return image;}
   string appFolder=Path.Combine(cache,p.SteamAppId);
   if(Directory.Exists(appFolder)){
    string[] files=Directory.GetFiles(appFolder,"*",SearchOption.AllDirectories);
    foreach(string pattern in new[]{"library_600x900","library_capsule","library_header"})foreach(string file in files.Where(f=>Path.GetFileName(f).StartsWith(pattern,StringComparison.OrdinalIgnoreCase))){var img=ReadImage(file);if(img!=null)return img;}
   }
  }catch{}return null;
 }
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){e.Handled=true;OnClick(EventArgs.Empty);}}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
  float s=g.DpiX/96f;float inset=5*s;var frame=new RectangleF(inset,inset,Width-2*inset,Height-78*s-inset);
  var shadow=frame;shadow.Offset(0,4*s);
  using(var path=UiShape.Round(shadow,19*s))using(var brush=new SolidBrush(Color.FromArgb(8,10,17)))g.FillPath(brush,path);
  using(var path=UiShape.Round(frame,19*s)){
   using(var brush=new SolidBrush(hover?Color.FromArgb(38,43,61):Color.FromArgb(27,32,45)))g.FillPath(brush,path);
   using(var pen=new Pen(Selected?Color.FromArgb(149,132,231):hover||Focused?Color.FromArgb(104,111,137):Color.FromArgb(55,61,77),Selected?2*s:s))g.DrawPath(pen,path);
  }
  var box=frame;box.Inflate(-7*s,-7*s);
  using(var path=UiShape.Round(box,13*s)){
   var state=g.Save();g.SetClip(path);
   if(artwork!=null){
    float factor=Store.Supported(Profile)?Math.Min(box.Width/artwork.Width,box.Height/artwork.Height):Math.Max(box.Width/artwork.Width,box.Height/artwork.Height);
    float w=artwork.Width*factor,h=artwork.Height*factor;g.DrawImage(artwork,new RectangleF(box.X+(box.Width-w)/2,box.Y+(box.Height-h)/2,w,h));
   }else{
    using(var brush=new LinearGradientBrush(box,Color.FromArgb(48,50,75),Color.FromArgb(23,29,41),55))g.FillRectangle(brush,box);
    using(var f=new Font(Font.FontFamily,22,FontStyle.Bold))TextRenderer.DrawText(g,Profile.Name.Substring(0,Math.Min(2,Profile.Name.Length)).ToUpperInvariant(),f,Rectangle.Round(box),Color.FromArgb(167,170,196),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
   }
   g.Restore(state);
  }
  int titleY=(int)(frame.Bottom+10*s);
  TextRenderer.DrawText(g,Profile.Name,Font,new Rectangle((int)(4*s),titleY,Width-(int)(8*s),(int)(38*s)),Color.FromArgb(233,235,244),TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
  using(var font=new Font(Font.FontFamily,8)){
   string badge=L.T((PackDownloads.Available(Profile)||EventPacks.Dedicated(Profile))?"듀얼센스 지원":"글로벌 적용");
   int w=Math.Min(Width-(int)(12*s),TextRenderer.MeasureText(g,badge,font).Width+(int)(18*s));var badgeBox=new Rectangle((Width-w)/2,Height-(int)(25*s),w,(int)(20*s));
   using(var path=UiShape.Round(badgeBox,10*s))using(var brush=new SolidBrush(Profile.HasEffects?Color.FromArgb(30,53,53):Color.FromArgb(29,33,46)))g.FillPath(brush,path);
   TextRenderer.DrawText(g,badge,font,badgeBox,Profile.HasEffects?Color.FromArgb(123,219,190):Color.FromArgb(153,162,184),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  }
 }
 protected override void Dispose(bool disposing){if(disposing&&artwork!=null)artwork.Dispose();base.Dispose(disposing);}
}
}
