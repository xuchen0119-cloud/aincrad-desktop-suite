using System;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Markup;

public static class SuiteProgram {
  static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
  static readonly string Local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  static Window window;
  static TextBlock status, next, engineLabel, themeLabel, compatibility;
  static StackPanel controls;
  static Button primary, themeRestore, cursorRestore;
  static ComboBox schemes;
  static CheckBox reduced;
  static SuiteState current;
  static bool busy, refreshing;
  [STAThread] public static int Main(string[] args) {
    if(Array.IndexOf(args,"--self-test")>=0){
      string[] required={"launcher/AincradLauncher.exe","launcher/start-managed-skin.ps1","theme/theme.json","theme/theme.css","theme/background.png","Aincrad-Theme-v2.8.zip","pet/yui/spritesheet.png","scripts/Install-Theme.ps1","scripts/Install-Cursors.ps1","README.md"};
      foreach(string path in required)if(!File.Exists(Path.Combine(Root,path.Replace('/',Path.DirectorySeparatorChar)))){Console.WriteLine("Missing package component: "+path);return 1;}
      Console.WriteLine("All suite components are present.");return 0;
    }
    try {
      window=BuildWindow();
      if(args.Length>=2 && args[0]=="--export-preview"){
        string scenario=args.Length>2?args[2]:"ready";
        ApplyState(new SuiteState{EngineInstalled=scenario!="new",EngineSupported=true,EngineVersion=scenario=="new"?"":"1.5.20",ThemeCurrent=scenario=="ready",ThemeBackup=scenario=="ready",CursorBackup=scenario=="ready"});
        FrameworkElement content=(FrameworkElement)window.Content;
        int width=args.Length>3?Int32.Parse(args[3]):1000,height=args.Length>4?Int32.Parse(args[4]):760;
        content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
        RenderTargetBitmap bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(content);
        PngBitmapEncoder encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(FileStream output=File.Create(args[1]))encoder.Save(output);
        return 0;
      }
      window.Loaded+=delegate{Refresh();};
      window.Activated+=delegate{Refresh();};
      new Application().Run(window);return 0;
    } catch(Exception ex){MessageBox.Show(ex.Message,"艾恩葛朗特整合包");return 1;}
  }
  static Brush Color(string value){return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(value));}
  static TextBlock Text(string value,double size,string color){return new TextBlock{Text=value,FontSize=size,Foreground=Color(color),TextWrapping=TextWrapping.Wrap};}
  static Border Card(UIElement child){return new Border{Background=Color("#102136"),BorderBrush=Color("#294459"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Padding=new Thickness(22),Child=child};}
  static TextBlock Label(StackPanel parent,string value,double size,string color,Thickness margin){TextBlock t=Text(value,size,color);t.Margin=margin;parent.Children.Add(t);return t;}
  static Window BuildWindow(){
    Grid grid=new Grid{Background=Color("#081320")};
    grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(174)});
    grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
    grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
    Grid hero=new Grid();
    hero.Background=new ImageBrush(new BitmapImage(new Uri(Path.Combine(Root,"theme","background.png")))){Stretch=Stretch.UniformToFill,AlignmentY=AlignmentY.Center};
    hero.Children.Add(new Border{Background=new LinearGradientBrush(System.Windows.Media.Color.FromArgb(245,8,19,32),System.Windows.Media.Color.FromArgb(60,8,19,32),0)});
    StackPanel heading=new StackPanel{Margin=new Thickness(30,24,30,22)};
    Label(heading,"AINCRAD   /   DESKTOP COLLECTION",11,"#A1D8EA",new Thickness());
    Label(heading,"从这里，进入艾恩葛朗特。",28,"#F1F7FC",new Thickness(0,12,0,9));
    Label(heading,"清晰壁纸  /  透明消息  /  剑光指针  /  结衣陪伴",13,"#C4D8E5",new Thickness());hero.Children.Add(heading);grid.Children.Add(hero);
    ScrollViewer scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);grid.Children.Add(scroll);
    controls=new StackPanel{Margin=new Thickness(24,18,24,12)};scroll.Content=controls;
    Grid overview=new Grid();overview.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.55,GridUnitType.Star)});overview.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
    StackPanel launch=new StackPanel();
    Label(launch,"01  /  界面与开屏",12,"#9CCDDD",new Thickness());
    next=Label(launch,"正在检查安装状态……",21,"#EEF7FC",new Thickness(0,12,0,5));
    themeLabel=Label(launch,"检测仅查看安装文件，不会自动更改设置。",12,"#B6CBDA",new Thickness(0,0,0,14));
    WrapPanel actions=new WrapPanel();
    primary=Button("正在检测……",delegate{PrimaryAction();},true);primary.IsEnabled=false;actions.Children.Add(primary);
    actions.Children.Add(Button("仅预览开屏",delegate{Start(Path.Combine(Root,"launcher","AincradLauncher.exe"),"--preview");},false));launch.Children.Add(actions);
    reduced=new CheckBox{Content="直接启动，减少动态效果",Foreground=Color("#C0D4E1"),Margin=new Thickness(0,12,0,12)};launch.Children.Add(reduced);
    WrapPanel maintenance=new WrapPanel();maintenance.Children.Add(Button("重新检测",delegate{Refresh();},false));
    themeRestore=Button("恢复先前界面",delegate{RunScript("Restore-Theme.ps1","");},false);themeRestore.IsEnabled=false;maintenance.Children.Add(themeRestore);launch.Children.Add(maintenance);
    Border launchCard=Card(launch);launchCard.Margin=new Thickness(0,0,14,0);overview.Children.Add(launchCard);
    StackPanel guide=new StackPanel();Label(guide,"首次使用路线",16,"#ECF4FA",new Thickness(0,0,0,14));
    engineLabel=Label(guide,"○  检查 Dream Skin",13,"#B8CCDA",new Thickness(0,0,0,9));
    Label(guide,"02  导入本包的界面主题",13,"#B8CCDA",new Thickness(0,0,0,9));
    Label(guide,"03  使用左侧 LINK START 入口",13,"#B8CCDA",new Thickness(0,0,0,16));
    compatibility=Label(guide,"",12,"#8FAFC3",new Thickness(0,0,0,12));
    guide.Children.Add(Button("安装与恢复说明",delegate{Open(Path.Combine(Root,"README.md"));},false));
    Border guideCard=Card(guide);Grid.SetColumn(guideCard,1);overview.Children.Add(guideCard);controls.Children.Add(overview);
    Grid extras=new Grid{Margin=new Thickness(0,14,0,0)};extras.ColumnDefinitions.Add(new ColumnDefinition());extras.ColumnDefinitions.Add(new ColumnDefinition());
    StackPanel cursor=new StackPanel();Label(cursor,"02  /  剑光光标",17,"#E4F2FA",new Thickness(0,0,0,7));
    Label(cursor,"三套完整方案，应用前自动保留原设置。",12,"#ABC3D4",new Thickness(0,0,0,12));
    schemes=new ComboBox{Height=32,SelectedIndex=0,MaxWidth=280,HorizontalAlignment=HorizontalAlignment.Left,MinWidth=220};
    schemes.Items.Add("冰蓝剑光 · 推荐");schemes.Items.Add("小光剑");schemes.Items.Add("银翼");cursor.Children.Add(schemes);
    WrapPanel cursorActions=new WrapPanel{Margin=new Thickness(0,12,0,0)};
    cursorActions.Children.Add(Button("应用光标",delegate{string[] names={"frostblade","lightsaber","silverwing"};RunScript("Install-Cursors.ps1","-Scheme "+names[schemes.SelectedIndex]);},false));
    cursorRestore=Button("恢复上一套",delegate{RunScript("Restore-Cursors.ps1","");},false);cursorRestore.IsEnabled=false;cursorActions.Children.Add(cursorRestore);cursor.Children.Add(cursorActions);
    Border cursorCard=Card(cursor);cursorCard.Margin=new Thickness(0,0,14,0);extras.Children.Add(cursorCard);
    StackPanel pet=new StackPanel();Label(pet,"03  /  结衣 Yui",17,"#E4F2FA",new Thickness(0,0,0,7));
    Label(pet,"九组动作与十六方向视线，陪伴你的下一段冒险。",12,"#ABC3D4",new Thickness(0,0,0,9));
    Label(pet,"需在支持桌宠的 ChatGPT Work 中单独导入。",12,"#8FAFC3",new Thickness(0,0,0,14));
    WrapPanel petActions=new WrapPanel();petActions.Children.Add(Button("导入结衣",delegate{Open(Path.Combine(Root,"pet","yui","IMPORT.md"));},false));petActions.Children.Add(Button("查看动作",delegate{Open(Path.Combine(Root,"pet","yui","preview","all-states.gif"));},false));pet.Children.Add(petActions);
    Border petCard=Card(pet);Grid.SetColumn(petCard,1);extras.Children.Add(petCard);controls.Children.Add(extras);
    status=Text("免费同人分享 · 无广告 · 无遥测    /    v3.1.0",12,"#A9C5D7");status.Margin=new Thickness(28,10,28,17);Grid.SetRow(status,2);grid.Children.Add(status);
    return new Window{Title="艾恩葛朗特桌面整合包 3.1.0",Width=1018,Height=810,MinWidth=760,MinHeight=620,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=Color("#081320"),Content=grid,FontFamily=new FontFamily("Microsoft YaHei UI"),UseLayoutRounding=true};
  }
  static Button Button(string title,Action action,bool main){
    Button b=new Button{Content=title,Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,8,5),MinHeight=35,Foreground=Color(main?"#061522":"#E2F0F8"),Background=Color(main?"#A9DCEB":"#19364A"),BorderBrush=Color(main?"#C5EDF7":"#385A70"),BorderThickness=new Thickness(1),FontWeight=main?FontWeights.SemiBold:FontWeights.Normal};
    b.Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border x:Name='surface' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='4' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='surface' Property='Opacity' Value='0.82'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='surface' Property='BorderBrush' Value='#E2F7FF'/><Setter TargetName='surface' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Foreground' Value='#8298A8'/><Setter TargetName='surface' Property='Background' Value='#142434'/><Setter TargetName='surface' Property='BorderBrush' Value='#293D50'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
    b.Click+=delegate{try{action();}catch(Exception ex){status.Text="操作未完成。";MessageBox.Show(ex.Message,"操作未完成");}};return b;
  }
  static void ApplyState(SuiteState value){
    current=value;primary.Content=value.NextAction;primary.IsEnabled=!busy;
    engineLabel.Text=value.EngineInstalled?"✓  Dream Skin "+value.EngineVersion+" 已安装":"01  安装 Dream Skin";
    next.Text=!value.EngineInstalled?"先准备换肤工具":!value.ThemeCurrent?"下一步，安装界面主题":"可以出发了，LINK START";
    themeLabel.Text=!value.EngineInstalled?"本包需要 Dream Skin，点击下方进入官方项目。":!value.ThemeCurrent?"主题尚未安装，或与当前包的内容不同。":"主题文件与本包一致；启动后将检查实际显示。";
    compatibility.Text=value.EngineInstalled?(value.EngineSupported?"支持完整主题、星夜侧栏与外框。":"该版本仅导入基础主题，跳过未验证的侧栏与外框扩展。") :"安装完成后返回这里，点击重新检测。";
    if(value.Problem!=""){themeLabel.Text=value.Problem;primary.IsEnabled=false;}
    themeRestore.IsEnabled=value.ThemeBackup;cursorRestore.IsEnabled=value.CursorBackup;
  }
  static void Refresh(){
    if(busy||refreshing)return;refreshing=true;
    BackgroundWorker worker=new BackgroundWorker();
    worker.DoWork+=delegate(object sender,DoWorkEventArgs e){e.Result=SuiteState.Inspect(Root,Local);};
    worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){refreshing=false;if(e.Error!=null){status.Text="状态检测未完成，请重新检测。";}else ApplyState((SuiteState)e.Result);};worker.RunWorkerAsync();
  }
  static void PrimaryAction(){
    if(current==null)return;
    if(!current.EngineInstalled){Open("https://github.com/Fei-Away/Codex-Dream-Skin/releases");return;}
    if(!current.ThemeCurrent){RunScript("Install-Theme.ps1","");return;}
    RunProcess(Path.Combine(Root,"launcher","AincradLauncher.exe"),reduced.IsChecked==true?"--reduced-motion":"",false);
  }
  static void Open(string target){Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}
  static void Start(string path,string arguments){Process.Start(new ProcessStartInfo(path,arguments){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(path)});}
  static void RunScript(string name,string arguments){
    string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe");
    RunProcess(shell,"-NoProfile -STA -ExecutionPolicy RemoteSigned -File \""+Path.Combine(Root,"scripts",name)+"\" "+arguments,true);
  }
  static void RunProcess(string path,string arguments,bool script){
    busy=true;controls.IsEnabled=false;status.Text=script?"正在应用设置，请稍候……":"正在启动；开屏后会继续显示启动状态……";
    BackgroundWorker worker=new BackgroundWorker();
    worker.DoWork+=delegate(object sender,DoWorkEventArgs e){
      ProcessStartInfo info=new ProcessStartInfo(path,arguments){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Root,RedirectStandardOutput=script,RedirectStandardError=script};
      using(Process p=Process.Start(info)){
        if(script){var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();p.WaitForExit();e.Result=output.Result+error.Result;}
        else p.WaitForExit();
        if(p.ExitCode!=0)throw new InvalidOperationException(script?Convert.ToString(e.Result):"启动未完成，请按启动窗口中的提示处理后再试。");
      }
    };
    worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){
      busy=false;controls.IsEnabled=true;
      if(e.Error!=null){status.Text="操作未完成，可按提示处理后重试。";if(script)MessageBox.Show(e.Error.Message,"艾恩葛朗特 · 操作提示");}
      else status.Text=script?"设置已保存。界面主题是否显示，将在启动时进一步检查。":"应用窗口与皮肤显示已验证。";
      Refresh();
    };worker.RunWorkerAsync();
  }
}
