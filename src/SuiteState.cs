using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

// File-only inspection: detecting installation is not proof of a visible skin.
public sealed class SuiteState {
  public bool EngineInstalled, EngineSupported, ThemeCurrent, ThemeBackup, CursorBackup;
  public string EngineVersion = "", Problem = "";
  public string NextAction {
    get { return !EngineInstalled ? "获取 Dream Skin" : !ThemeCurrent ? "安装 / 更新皮肤" : "LINK START · 启动"; }
  }
  public static SuiteState Inspect(string bundle, string localData) {
    SuiteState result = new SuiteState();
    string engineRoot = Path.Combine(localData,"CodexDreamSkin");
    string engine = Path.Combine(engineRoot,"engine");
    try {
      string[] required = {"VERSION","scripts/common-windows.ps1","scripts/theme-windows.ps1","scripts/start-dream-skin.ps1","scripts/injector.mjs"};
      result.EngineInstalled = true;
      foreach (string file in required) result.EngineInstalled &= File.Exists(Path.Combine(engine,file.Replace('/',Path.DirectorySeparatorChar)));
      if (result.EngineInstalled) {
        result.EngineVersion = File.ReadAllText(Path.Combine(engine,"VERSION")).Trim();
        result.EngineSupported = result.EngineVersion == "1.5.20";
        result.ThemeCurrent = true;
        foreach(string installed in new[]{"active-theme", "themes/local-sao-aincrad-link-start"}) {
          string directory=Path.Combine(engineRoot,installed.Replace('/',Path.DirectorySeparatorChar));
          result.ThemeCurrent &= SameJson(Path.Combine(bundle,"theme","theme.json"),Path.Combine(directory,"theme.json"));
          result.ThemeCurrent &= SameFile(Path.Combine(bundle,"theme","theme.css"),Path.Combine(directory,"theme.css"));
          string image=Field(ReadJson(Path.Combine(directory,"theme.json")),"image");
          result.ThemeCurrent &= image!="" && Path.GetFileName(image)==image && SameFile(Path.Combine(bundle,"theme","background.png"),Path.Combine(directory,image));
        }
        if(result.ThemeCurrent && result.EngineSupported) {
          string cssPath=Path.Combine(engine,"assets","dream-skin.css");
          string extension=File.ReadAllText(Path.Combine(bundle,"theme-extension","aincrad-ui.css"));
          result.ThemeCurrent = File.Exists(cssPath) && File.ReadAllText(cssPath).Contains(extension);
        }
      }
      string runtime=Path.Combine(localData,"AincradSuite");
      result.ThemeBackup=ValidBackup(runtime,"last-theme-backup.txt","theme-backup-",true);
      result.CursorBackup=ValidBackup(runtime,"last-cursor-backup.txt","cursor-backup-",false);
    } catch(IOException) { result.Problem="安装文件暂时不可读，请稍后重新检测。"; result.ThemeCurrent=false; }
      catch(UnauthorizedAccessException) { result.Problem="无法读取安装状态，请检查当前用户的文件访问权限。"; result.ThemeCurrent=false; }
    return result;
  }
  static bool ValidBackup(string root,string pointer,string prefix,bool directory) {
    string path=Path.Combine(root,pointer); if(!File.Exists(path))return false;
    string name=File.ReadAllText(path).Trim();
    if(Path.GetFileName(name)!=name || !name.StartsWith(prefix,StringComparison.Ordinal))return false;
    return directory ? File.Exists(Path.Combine(root,name,"theme.json")) : File.Exists(Path.Combine(root,name));
  }
  static bool SameFile(string left,string right) {
    if(!File.Exists(left)||!File.Exists(right))return false;
    if(new FileInfo(left).Length!=new FileInfo(right).Length)return false;
    using(SHA256 hash=SHA256.Create())using(FileStream a=File.OpenRead(left))using(FileStream b=File.OpenRead(right))
      return Convert.ToBase64String(hash.ComputeHash(a))==Convert.ToBase64String(hash.ComputeHash(b));
  }
  static bool SameJson(string expected,string actual) {
    var left=ReadJson(expected);var right=ReadJson(actual);
    // Dream Skin assigns the imported artwork a generated filename.
    // Compare artwork bytes separately; do not mistake that rename for an update.
    if(left!=null)left.Remove("image");if(right!=null)right.Remove("image");
    return left!=null && right!=null && Includes(left,right);
  }
  static bool Includes(object expected,object actual) {
    var fields=expected as IDictionary<string,object>;
    if(fields!=null){
      var values=actual as IDictionary<string,object>;if(values==null)return false;
      foreach(var field in fields){object value;if(!values.TryGetValue(field.Key,out value)||!Includes(field.Value,value))return false;}
      return true;
    }
    return Object.Equals(expected,actual);
  }
  public static string ResolveArtwork(string launcherRoot) {
    string shared=Path.GetFullPath(Path.Combine(launcherRoot,"..","theme","background.png"));
    if(File.Exists(shared))return shared;
    string legacy=Path.Combine(launcherRoot,"theme","background.png");
    if(File.Exists(legacy))return legacy;
    throw new FileNotFoundException("壁纸文件缺失，请重新解压完整整合包。");
  }
  public static IDictionary<string,object> ReadJson(string path) {
    if(!File.Exists(path)||new FileInfo(path).Length>65536)return null;
    try { return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(path)); }
    catch(IOException){return null;}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}catch(UnauthorizedAccessException){return null;}
  }
  public static string Field(IDictionary<string,object> json,string key) {
    object value; return json!=null&&json.TryGetValue(key,out value)?Convert.ToString(value):"";
  }
  public static bool IsVerified(int exitCode, string path) {
    return exitCode==0 && Field(ReadJson(path),"phase")=="verified";
  }
}
