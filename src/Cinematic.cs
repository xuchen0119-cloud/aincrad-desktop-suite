using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Local vector film. Reuse the verified managed-profile startup path.
public static class AincradLauncher {
  static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
  static readonly string RuntimeRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AincradSuite", "runtime");
  const double Duration = 7.2;
  static bool launchRequested;
  static Mutex singleInstance;
  static bool ownsMutex;
  static string runStatusPath;
  static readonly Stopwatch helperClock = new Stopwatch();
  [STAThread]
  public static int Main(string[] args) {
    try {
      if(Array.IndexOf(args,"--self-test")>=0){
        SuiteState.ResolveArtwork(Root);
        if(!File.Exists(Path.Combine(Root,"start-managed-skin.ps1")))return 1;
        Console.WriteLine("Launcher artwork and helper are present.");return 0;
      }
      bool preview = Array.IndexOf(args, "--preview") >= 0;
      bool reduced = Array.IndexOf(args, "--reduced-motion") >= 0;
      bool exporting = args.Length >= 2 && (args[0] == "--export-stills" || args[0] == "--export-frames");
      bool full = preview || Array.IndexOf(args, "--full-intro") >= 0;
      Directory.CreateDirectory(RuntimeRoot);
      if (!preview && !exporting) {
        singleInstance = new Mutex(false, "Local\\AincradDreamSkinLauncher");
        try { ownsMutex = singleInstance.WaitOne(0); } catch (AbandonedMutexException) { ownsMutex = true; }
        if (!ownsMutex) { Journal("duplicate-click-ignored"); MessageBox.Show("已有启动流程正在运行，请返回已有启动窗口。","艾恩葛朗特"); return 3; }
      }
      Journal("launcher-start; preview=" + preview);
      if (reduced && !preview && !exporting) { StartSkinHelper(); return StartClient(); }
      if (!preview && !exporting) StartSkinHelper();
      var scene = new DiveScene(SuiteState.ResolveArtwork(Root));
      if (args.Length >= 2 && args[0] == "--export-stills") {
        ExportStills(scene, args[1]); return 0;
      }
      if (args.Length >= 2 && args[0] == "--export-frames") {
        ExportFrames(scene, args[1]); return 0;
      }
      var area = SystemParameters.WorkArea;
      var window = new Window {
        Title = preview ? "开屏预览 · 艾恩葛朗特" : "艾恩葛朗特 · LINK START", WindowStyle = WindowStyle.None,
        ResizeMode = ResizeMode.NoResize, Background = Brushes.Black,
        Content = scene, WindowStartupLocation = WindowStartupLocation.CenterScreen,
        Width = Math.Min(1280, area.Width * .92), Height = Math.Min(720, area.Height * .92),
        ShowInTaskbar = true, UseLayoutRounding = false
      };
      if (!preview) { window.WindowState = WindowState.Maximized; window.Topmost = true; }
      var clock = new Stopwatch();
      double previous = 0, filmTime = 0, fadeStart = -1, lastPoll = -1;
      bool helperEnded = preview;
      EventHandler frame = delegate {
        double t = clock.Elapsed.TotalSeconds;
        if (t - previous < 1.0 / 60.0) return;
        double delta = Math.Min(.10, t - previous); previous = t;
        if (!preview && t-lastPoll >= .2) {
          lastPoll = t; helperEnded = skinProcess != null && skinProcess.HasExited;
        }
        double pace = full ? 1 : (helperEnded && filmTime > 3.2 ? 1.65 : 1.28);
        filmTime = Math.Min(Duration, filmTime + delta * pace);
        scene.Time = filmTime;
        scene.InvalidateVisual();
        // Finish the film on time; a responsive status window handles slow startup.
        if (filmTime >= Duration) {
          if (fadeStart < 0) fadeStart = t;
          window.Opacity = Math.Max(0, 1-(t-fadeStart)/.26);
          if (t-fadeStart < .26) return;
          launchRequested = !preview;
          Journal(preview ? "preview-complete" : "animation-complete; opening-client");
          window.Close();
        }
      };
      window.KeyDown += delegate(object sender, KeyEventArgs e) {
        if (e.Key == Key.Escape) { launchRequested = !preview; window.Close(); }
      };
      window.Loaded += delegate {
        clock.Start(); CompositionTarget.Rendering += frame; window.Activate();
      };
      window.Closed += delegate { CompositionTarget.Rendering -= frame; clock.Stop(); };
      window.ShowDialog();
      return preview ? 0 : launchRequested ? StartClient() : 3;
    } catch (Exception error) {
      File.WriteAllText(Path.Combine(RuntimeRoot, "launcher-error.log"), error.ToString(), Encoding.UTF8);
      if(skinProcess!=null){Journal("intro-unavailable; continuing-managed-startup");return StartClient();}
      MessageBox.Show("开屏未能打开，详细结果已保存到 launcher-error.log。", "艾恩葛朗特", MessageBoxButton.OK, MessageBoxImage.Information);
      return 1;
    } finally {
      if (ownsMutex) singleInstance.ReleaseMutex();
      if (singleInstance != null) singleInstance.Dispose();
    }
  }
  static void RenderFrame(DiveScene scene, string path, double time, int width, int height) {
    scene.Time = time;
    scene.Measure(new Size(width, height));
    scene.Arrange(new Rect(0, 0, width, height));
    scene.InvalidateVisual(); scene.UpdateLayout();
    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(scene);
    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using (var file = File.Create(path)) encoder.Save(file);
  }
  static void ExportStills(DiveScene scene, string directory) {
    Directory.CreateDirectory(directory);
    double[] times = { 1.8, 3.45, 4.95, 6.65 };
    for (int i = 0; i < times.Length; i++)
      RenderFrame(scene, Path.Combine(directory, "opening-" + (i + 1) + ".png"), times[i], 1280, 720);
  }
  static void ExportFrames(DiveScene scene, string directory) {
    Directory.CreateDirectory(directory);
    for (int i = 0; i < 144; i++)
      RenderFrame(scene, Path.Combine(directory, i.ToString("D4") + ".png"), i / 20.0, 800, 450);
  }
  static readonly object journalLock = new object();
  static void Journal(string message) {
    lock(journalLock) File.AppendAllText(Path.Combine(RuntimeRoot,"launch-journal.log"), DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine, Encoding.UTF8);
  }
  static Process skinProcess;
  static void StartSkinHelper() {
    string helper=Path.Combine(Root,"start-managed-skin.ps1");
    if(!File.Exists(helper)) throw new FileNotFoundException("The managed skin helper is missing.",helper);
    string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe");
    runStatusPath=Path.Combine(RuntimeRoot,"launch-"+Guid.NewGuid().ToString("N")+".json");
    var info=new ProcessStartInfo(powershell) {
      Arguments="-NoProfile -STA -ExecutionPolicy RemoteSigned -File \""+helper+"\" -StatusPath \""+runStatusPath+"\"",
      UseShellExecute=false, CreateNoWindow=true, WorkingDirectory=Root
    };
    skinProcess=Process.Start(info);
    helperClock.Start();
    Journal("managed-skin-dispatched; pid="+skinProcess.Id);
  }
  static int StartClient() {
    if(skinProcess==null) StartSkinHelper();
    int result=2;
    if(skinProcess.HasExited && SuiteState.IsVerified(skinProcess.ExitCode,runStatusPath)){
      Journal("client-start-complete; visible-skin-verified");skinProcess.Dispose();return 0;
    }
    var heading=new TextBlock{Text="正在进入艾恩葛朗特",FontSize=23,Foreground=Brushes.White,Margin=new Thickness(0,0,0,14)};
    var detail=new TextBlock{Text="正在连接应用并检查皮肤显示……",FontSize=14,Foreground=new SolidColorBrush(Color.FromRgb(183,213,231)),TextWrapping=TextWrapping.Wrap};
    var elapsed=new TextBlock{FontSize=12,Foreground=Brushes.LightSlateGray,Margin=new Thickness(0,12,0,16)};
    var progress=new ProgressBar{IsIndeterminate=true,Height=3,Margin=new Thickness(0,0,0,18)};
    var actions=new StackPanel{Orientation=Orientation.Horizontal,Visibility=Visibility.Collapsed};
    var logs=new Button{Content="查看本机诊断",Padding=new Thickness(12,6,12,6),Margin=new Thickness(0,0,10,0)};
    logs.Click+=delegate{Process.Start(new ProcessStartInfo(RuntimeRoot){UseShellExecute=true});};actions.Children.Add(logs);
    var close=new Button{Content="关闭提示",Padding=new Thickness(12,6,12,6)};actions.Children.Add(close);
    var content=new StackPanel{Margin=new Thickness(28)};content.Children.Add(heading);content.Children.Add(detail);content.Children.Add(elapsed);content.Children.Add(progress);content.Children.Add(actions);
    var waiting=new Window{Title="艾恩葛朗特 · 启动状态",Width=530,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=new SolidColorBrush(Color.FromRgb(8,21,39)),FontFamily=new FontFamily("Microsoft YaHei UI"),Content=content};
    close.Click+=delegate{waiting.Close();};
    var poll=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};
    poll.Tick+=delegate{
      var state=SuiteState.ReadJson(runStatusPath);
      string phase=SuiteState.Field(state,"phase");
      elapsed.Text="已用时 "+(int)helperClock.Elapsed.TotalSeconds+" 秒";
      if(!skinProcess.HasExited && helperClock.Elapsed.TotalSeconds<120){
        detail.Text=phase=="applying"?"应用正在准备皮肤环境；若出现应用确认窗口，请在那里继续。":phase=="verifying"?"应用已打开，正在检查皮肤是否实际显示。":"正在连接应用并检查已安装主题……";return;
      }
      poll.Stop();progress.Visibility=Visibility.Collapsed;
      if(skinProcess.HasExited && SuiteState.IsVerified(skinProcess.ExitCode,runStatusPath)){
        result=0;Journal("client-start-complete; visible-skin-verified");waiting.Close();return;
      }
      result=skinProcess.HasExited?1:2;actions.Visibility=Visibility.Visible;
      heading.Text=skinProcess.HasExited?"启动尚未完成":"启动仍在后台进行";
      string message=SuiteState.Field(state,"detail");
      detail.Text=skinProcess.HasExited?(message==""?"未收到皮肤显示确认。请返回整合入口检查安装，或查看本机诊断。":message):"等待已超过两分钟。后台流程仍会继续，请勿重复启动；可查看本机诊断，或稍后返回整合入口。";
      Journal("managed-skin-not-verified; result="+result);
    };
    waiting.Loaded+=delegate{poll.Start();};waiting.Closed+=delegate{poll.Stop();};waiting.ShowDialog();
    skinProcess.Dispose();return result;
  }

}

public sealed class DiveScene : FrameworkElement {
  public double Time;
  readonly BitmapImage art;
  readonly Typeface light = new Typeface(new FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.Light, FontStretches.SemiCondensed);
  readonly Typeface chinese = new Typeface("Microsoft YaHei UI");
  readonly Random random = new Random(4106);
  readonly Point[] stars = new Point[180];
  readonly double[] starSize = new double[180];
  static readonly Dictionary<int, Brush> inks = new Dictionary<int, Brush>();
  static readonly Dictionary<string, Pen> pens = new Dictionary<string, Pen>();
  readonly Dictionary<string, FormattedText> textCache = new Dictionary<string, FormattedText>();
  readonly StreamGeometry[,] fibers = new StreamGeometry[64, 80];
  readonly DrawingGroup fiberBed = new DrawingGroup();
  readonly DrawingGroup ticks = new DrawingGroup();
  readonly StreamGeometry swordShape = new StreamGeometry();
  readonly StreamGeometry[] arcs = new StreamGeometry[3];
  readonly LinearGradientBrush baseGradient = new LinearGradientBrush(Color.FromRgb(2,7,16),Color.FromRgb(8,19,34),90);
  readonly RadialGradientBrush glowBrush = new RadialGradientBrush();
  readonly LinearGradientBrush worldShade = new LinearGradientBrush();
  const double Cx = 800, Cy = 405;
  public DiveScene(string imagePath) {
    art = new BitmapImage(); art.BeginInit(); art.CacheOption = BitmapCacheOption.OnLoad;
    art.UriSource = new Uri(imagePath); art.EndInit(); art.Freeze();
    for (int i = 0; i < stars.Length; i++) {
      stars[i] = new Point(random.NextDouble()*1600, random.NextDouble()*900);
      starSize[i] = .6 + random.NextDouble()*1.3;
    }
    SnapsToDevicePixels = false;
    baseGradient.Freeze();
    glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255,133,192,218),0));
    glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(60,50,106,156),.45));
    glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0,10,40,70),1)); glowBrush.Freeze();
    worldShade.StartPoint=new Point(0,0); worldShade.EndPoint=new Point(0,1);
    worldShade.GradientStops.Add(new GradientStop(Color.FromArgb(0,3,13,27),.4));
    worldShade.GradientStops.Add(new GradientStop(Color.FromArgb(205,3,13,27),1)); worldShade.Freeze();
    BuildDetails(); BuildFibers();
  }
  static double Clamp(double x) { return Math.Max(0, Math.Min(1, x)); }
  static double Smooth(double a, double b, double x) { double v=Clamp((x-a)/(b-a)); return v*v*(3-2*v); }
  static Brush Ink(byte r, byte g, byte b, double opacity) {
    // Quantized alpha bounds cache growth during continuous motion.
    int a=(int)(Clamp(opacity)*63+.5)*4; if(a>255)a=255;
    int key=(a<<24)|(r<<16)|(g<<8)|b;
    Brush cached; if(inks.TryGetValue(key,out cached)) return cached;
    var brush = new SolidColorBrush(Color.FromArgb((byte)a,r,g,b)); brush.Freeze(); inks[key]=brush; return brush;
  }
  static Pen Line(byte r, byte g, byte b, double opacity, double width) {
    width=Math.Round(width*4)/4;
    Brush brush=Ink(r,g,b,opacity);
    string key=((SolidColorBrush)brush).Color.ToString()+":"+width.ToString(CultureInfo.InvariantCulture);
    Pen pen; if(pens.TryGetValue(key,out pen)) return pen;
    pen = new Pen(brush, width); pen.Freeze(); pens[key]=pen; return pen;
  }
  static Point Polar(double radius, double angle, double yScale) {
    return new Point(Cx+Math.Cos(angle)*radius, Cy+Math.Sin(angle)*radius*yScale);
  }
  void Text(DrawingContext dc, string text, double size, double x, double y, Brush brush, bool centered, bool cn) {
    var ft = Layout(text,size,cn);
    dc.PushOpacity(((SolidColorBrush)brush).Color.A/255.0);
    dc.DrawText(ft,new Point(centered ? x-ft.Width/2 : x,y));
    dc.Pop();
  }
  FormattedText Layout(string text,double size,bool cn) {
    string key=text+":"+size.ToString(CultureInfo.InvariantCulture)+":"+cn;
    FormattedText ft; if(textCache.TryGetValue(key,out ft))return ft;
    ft=new FormattedText(text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,cn?chinese:light,size,Ink(222,235,242,1),1.0);
    textCache[key]=ft; return ft;
  }
  void Tracked(DrawingContext dc, string text, double size, double spacing, double y, double opacity) {
    double width = 0;
    foreach(char c in text) {
      var ft = Layout(c.ToString(),size,false);
      width += ft.WidthIncludingTrailingWhitespace + spacing;
    }
    double x = Cx-(width-spacing)/2;
    dc.PushOpacity(opacity);
    foreach(char c in text) {
      var ft = Layout(c.ToString(),size,false);
      dc.DrawText(ft,new Point(x,y)); x += ft.WidthIncludingTrailingWhitespace+spacing;
    }
    dc.Pop();
  }
  void Glow(DrawingContext dc, double x, double y, double rx, double ry, double opacity) {
    dc.PushOpacity(Clamp(opacity)); dc.DrawEllipse(glowBrush,null,new Point(x,y),rx,ry); dc.Pop();
  }
  void Arc(DrawingContext dc, double radius, double from, double sweep, double alpha, double thickness) {
    int index=sweep==32?0:sweep==52?1:2;
    dc.PushTransform(new RotateTransform(from,Cx,Cy));
    dc.PushTransform(new ScaleTransform(radius/240,radius/240,Cx,Cy));
    dc.DrawGeometry(null,Line(158,210,229,alpha,thickness*240/radius),arcs[index]); dc.Pop();dc.Pop();
  }
  void Sword(DrawingContext dc, double angle, double alpha) {
    dc.PushTransform(new RotateTransform(angle,Cx,Cy));
    dc.DrawGeometry(Ink(164,223,242,alpha*.08),Line(212,232,241,alpha,1.5),swordShape);
    dc.DrawLine(Line(196,243,255,alpha,2),new Point(Cx-23,Cy+39),new Point(Cx+23,Cy+39));
    dc.DrawLine(Line(164,233,255,alpha,5),new Point(Cx,Cy+44),new Point(Cx,Cy+70));
    dc.DrawEllipse(Ink(223,251,255,alpha),null,new Point(Cx,Cy+75),4,4);
    dc.Pop();
  }
  void Hud(DrawingContext dc, double t, double alpha) {
    if(alpha<=0) return;
    dc.PushOpacity(alpha);
    double spread=Smooth(0,1.5,t);
    dc.PushTransform(new ScaleTransform(.62+.38*spread,.62+.38*spread,Cx,Cy));
    Glow(dc,Cx,Cy,340,340,.17);
    dc.DrawEllipse(null,Line(70,162,204,.22,1),new Point(Cx,Cy),272,272);
    dc.DrawEllipse(null,Line(104,214,248,.36,1),new Point(Cx,Cy),252,252);
    dc.DrawEllipse(null,Line(72,174,219,.24,1),new Point(Cx,Cy),197,197);
    dc.DrawEllipse(null,Line(72,174,219,.16,1),new Point(Cx,Cy),135,135);
    dc.DrawDrawing(ticks);
    for(int j=0;j<6;j++) Arc(dc,240,j*60+t*23,32,j%2==0?.8:.3,2.5);
    for(int j=0;j<4;j++) Arc(dc,211,j*90-t*36,52,.55,1.3);
    for(int j=0;j<3;j++) Arc(dc,151,j*120+t*51,68,.25,1);
    for(int j=0;j<4;j++) {
      double a=j*Math.PI/2 +Math.PI/4;
      Point p=Polar(287,a,1);
      dc.PushTransform(new RotateTransform(j*90+45,p.X,p.Y));
      dc.DrawRectangle(Ink(173,240,255,.8),null,new Rect(p.X-3,p.Y-3,6,6)); dc.Pop();
    }
    Sword(dc,-35,.9); Sword(dc,35,.9);
    Glow(dc,Cx,Cy,40,40,.22);
    dc.Pop();
    double side=Smooth(.5,1.7,t);
    dc.PushOpacity(side);
    var fine=Line(102,212,250,.35,1);
    dc.DrawLine(fine,new Point(166,340),new Point(405,340));
    dc.DrawLine(fine,new Point(405,340),new Point(460,392));
    dc.DrawLine(fine,new Point(1138,392),new Point(1195,340));
    dc.DrawLine(fine,new Point(1195,340),new Point(1434,340));
    Text(dc,"N E R V E G E A R",17,166,305,Ink(168,222,242,.8),false,false);
    Text(dc,"FULLDIVE  /  SYSTEM",12,166,357,Ink(110,174,203,.68),false,false);
    Text(dc,"A I N C R A D",17,1237,305,Ink(168,222,242,.8),false,false);
    Text(dc,"VIRTUAL  REALITY",12,1237,357,Ink(110,174,203,.68),false,false);
    dc.Pop(); dc.Pop();
  }
  void BuildDetails() {
    using(var d=ticks.Open()) for(int i=0;i<120;i++) {
      double a=i*Math.PI/60;
      d.DrawLine(Line(171,202,217,i%5==0?.55:.20,i%5==0?1.25:.75),Polar(i%5==0?259:267,a,1),Polar(273,a,1));
    }
    ticks.Freeze();
    using(var c=swordShape.Open()) {
      c.BeginFigure(new Point(Cx,Cy-107),true,true);
      c.PolyLineTo(new Point[]{new Point(Cx+7,Cy-80),new Point(Cx+5,Cy+37),new Point(Cx-5,Cy+37),new Point(Cx-7,Cy-80)},true,false);
    }
    swordShape.Freeze();
    double[] sweeps={32,52,68};
    for(int i=0;i<3;i++) {
      arcs[i]=new StreamGeometry(); using(var c=arcs[i].Open()) {
        c.BeginFigure(Polar(240,0,1),false,false);
        c.ArcTo(Polar(240,sweeps[i]*Math.PI/180,1),new Size(240,240),0,false,SweepDirection.Clockwise,true,false);
      } arcs[i].Freeze();
    }
  }
  Point FiberPoint(int cable,double depth) {
    double radius=23+Math.Pow(depth,2.15)*1600;
    double angle=cable*Math.PI/32 + .36*Math.Sin(depth*4.4+cable*.045) + .20*depth;
    double bendX=130*Math.Sin((1-depth)*3.0)*(1-depth);
    double bendY=55*Math.Sin((1-depth)*4.5)*(1-depth);
    return new Point(Cx+bendX+Math.Cos(angle)*radius,Cy+bendY+Math.Sin(angle)*radius*.68);
  }
  void BuildFibers() {
    using(var d=fiberBed.Open()) for(int c=0;c<64;c++) {
      var full=new StreamGeometry();
      using(var path=full.Open()) {
        path.BeginFigure(FiberPoint(c,0),false,false);
        for(int j=1;j<=80;j++) path.LineTo(FiberPoint(c,j/80.0),true,false);
      }
      full.Freeze();
      d.DrawGeometry(null,Line(93,139,168,c%3==0?.16:.08,c%3==0?.75:.5),full);
      for(int j=0;j<80;j++) {
        var seg=new StreamGeometry(); using(var p=seg.Open()) {
          p.BeginFigure(FiberPoint(c,j/80.0),false,false);
          p.LineTo(FiberPoint(c,(j+1)/80.0),true,false);
        } seg.Freeze(); fibers[c,j]=seg;
      }
    }
    fiberBed.Freeze();
  }
  void FrameDetails(DrawingContext dc,double t,double alpha) {
    if(alpha<=0)return;
    dc.PushOpacity(alpha);
    Pen line=Line(172,200,220,.26,1);
    double extent=48+38*Smooth(.2,1.2,t);
    for(int i=0;i<4;i++) {
      double x=i%2==0?72:1528, y=i<2?76:824, sx=i%2==0?1:-1, sy=i<2?1:-1;
      dc.DrawLine(line,new Point(x,y+sy*22),new Point(x,y));
      dc.DrawLine(line,new Point(x,y),new Point(x+sx*extent,y));
      dc.DrawRectangle(Ink(215,194,150,.50),null,new Rect(x+(sx<0?-3:0),y+(sy<0?-3:0),3,3));
    }
    Text(dc,"FULLDIVE INTERFACE",11,96,86,Ink(159,184,202,.60),false,false);
    Text(dc,"01  /  AINCRAD",11,1360,86,Ink(159,184,202,.60),false,false);
    Text(dc,"SWORD ART ONLINE",10,96,798,Ink(159,184,202,.45),false,false);
    dc.DrawLine(Line(186,208,219,.12,1),new Point(96,780),new Point(310,780));
    dc.Pop();
  }
  void Tunnel(DrawingContext dc, double t, double alpha) {
    if(alpha<=0) return;
    dc.PushOpacity(alpha);
    double acceleration=Smooth(3.9,5.45,t);
    dc.PushTransform(new RotateTransform((t-3.65)*6,Cx,Cy));
    dc.DrawDrawing(fiberBed);
    for(int c=0;c<64;c++) {
      double head=(c*.61803398875+(t-3.65)*(.28+.18*acceleration)*(1+(c%7)*.06))%1;
      int length=13+c%9;
      byte r=c%11==0?(byte)233:c%7==0?(byte)170:(byte)175;
      byte g=c%11==0?(byte)219:c%7==0?(byte)180:(byte)217;
      for(int k=0;k<length;k++) {
        int j=(int)(head*80)-k; if(j<0||j>=80)continue;
        double z=j/80.0;
        double softness=Math.Sin(Math.PI*(k+.5)/length);
        double strength=softness*softness*Smooth(.04,.27,z)*(1-Smooth(.8,1,z));
        if(strength<.015)continue;
        dc.DrawGeometry(null,Line(r,g,238,strength*.10,5),fibers[c,j]);
        dc.DrawGeometry(null,Line(r,g,238,strength*.72,.75+z*1.5),fibers[c,j]);
      }
    }
    dc.Pop();
    Glow(dc,Cx+35,Cy,260,170,.12);
    Glow(dc,Cx+35,Cy,70,50,.18+acceleration*.18);
    dc.Pop();
  }
  protected override void OnRender(DrawingContext dc) {
    base.OnRender(dc);
    double t=Math.Min(Time,7.2);
    dc.DrawRectangle(Brushes.Black,null,new Rect(0,0,ActualWidth,ActualHeight));
    double screenScale=Math.Min(ActualWidth/1600.0,ActualHeight/900.0);
    dc.PushTransform(new MatrixTransform(screenScale,0,0,screenScale,(ActualWidth-1600*screenScale)/2,(ActualHeight-900*screenScale)/2));
    dc.PushClip(new RectangleGeometry(new Rect(0,0,1600,900)));
    dc.DrawRectangle(baseGradient,null,new Rect(0,0,1600,900));
    double reveal=Smooth(5.35,6.45,t);
    if(reveal>0) {
      dc.PushOpacity(reveal);
      double zoom=1.065-.065*Smooth(5.35,7.2,t);
      dc.PushTransform(new ScaleTransform(zoom,zoom,800,450));
      dc.DrawImage(art,new Rect(0,0,1600,900)); dc.Pop();
      dc.DrawRectangle(Ink(2,10,23,.27),null,new Rect(0,0,1600,900));
      dc.DrawRectangle(worldShade,null,new Rect(0,0,1600,900)); dc.Pop();
    }
    double starsAlpha=1-reveal*.6;
    for(int i=0;i<stars.Length;i++) {
      double pulse=.35+.2*Math.Sin(t*.7+i);
      dc.DrawEllipse(Ink(145,208,242,pulse*starsAlpha),null,stars[i],starSize[i],starSize[i]);
    }
    double hudAlpha=Smooth(0,.75,t)*(1-Smooth(2.75,3.8,t));
    FrameDetails(dc,t,Smooth(0,.8,t)*(1-Smooth(3.5,4.3,t))*.85);
    Hud(dc,t,hudAlpha);
    double scan=Smooth(.3,.8,t)*(1-Smooth(2.5,3.1,t));
    if(scan>0) {
      double y=150+(t*.32%1)*580;
      dc.DrawRectangle(Ink(70,196,250,.035*scan),null,new Rect(100,y-18,1400,36));
      dc.DrawLine(Line(119,226,255,.35*scan,1),new Point(180,y),new Point(1420,y));
    }
    double link=Smooth(2.65,3.15,t)*(1-Smooth(4.05,4.5,t));
    if(link>0) {
      dc.PushOpacity(link);
      Glow(dc,Cx,Cy,610,240,.2);
      double tracking=8+16*(1-Smooth(2.65,3.5,t));
      Tracked(dc,"LINK START",88,tracking,354,1);
      Text(dc,"S W O R D   A R T   O N L I N E",15,Cx,480,Ink(142,219,248,.8),true,false);
      double lineWidth=370*Smooth(2.8,3.6,t);
      dc.DrawLine(Line(134,230,255,.7,1),new Point(Cx-lineWidth,530),new Point(Cx+lineWidth,530));
      Glow(dc,Cx,530,440,5,.35);
      dc.Pop();
    }
    Tunnel(dc,t,Smooth(3.65,4.25,t)*(1-Smooth(5.6,6.2,t)));
    double flare=Smooth(5.05,5.4,t)*(1-Smooth(5.4,5.95,t));
    if(flare>0) {
      Glow(dc,Cx,Cy,1600,1100,flare*.28);
      Glow(dc,Cx,Cy,700,460,flare*.38);
    }
    double world=Smooth(6.0,6.55,t);
    if(world>0) {
      dc.PushOpacity(world);
      Tracked(dc,"AINCRAD",44,12,704,.95);
      Text(dc,"アインクラッド",15,Cx,771,Ink(197,231,246,.78),true,false);
      dc.DrawLine(Line(137,223,252,.52,1),new Point(636,816),new Point(964,816));
      dc.DrawEllipse(Ink(211,246,255,.9),null,new Point(800,816),3,3);
      dc.Pop();
    }
    // Fine cinematic letterboxing and a smooth entrance; no controls or hints.
    dc.DrawRectangle(Brushes.Black,null,new Rect(0,0,1600,26));
    dc.DrawRectangle(Brushes.Black,null,new Rect(0,874,1600,26));
    double entrance=1-Smooth(0,.5,t);
    if(entrance>0) dc.DrawRectangle(Ink(0,0,0,entrance),null,new Rect(0,0,1600,900));
    dc.Pop(); dc.Pop();
  }
}
