using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

public static class CursorNative {
  [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern IntPtr LoadCursorFromFileW(string path);
  [DllImport("user32.dll", SetLastError=true)]
  public static extern IntPtr LoadCursorW(IntPtr instance, IntPtr name);
  [DllImport("user32.dll", SetLastError=true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static extern bool DestroyCursor(IntPtr cursor);
  [DllImport("user32.dll", SetLastError=true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static extern bool SystemParametersInfoW(uint action,uint parameter,IntPtr value,uint flags);
  [DllImport("user32.dll", SetLastError=true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  static extern bool GetIconInfo(IntPtr cursor, out ICONINFO info);
  [DllImport("gdi32.dll", EntryPoint="GetObjectW", SetLastError=true)]
  static extern int GetObject(IntPtr obj,int count,out BITMAP bitmap);
  [DllImport("gdi32.dll", SetLastError=true)]
  static extern int GetBitmapBits(IntPtr bitmap,int count,byte[] pixels);
  [DllImport("gdi32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  static extern bool DeleteObject(IntPtr obj);
  [StructLayout(LayoutKind.Sequential)]
  struct ICONINFO { [MarshalAs(UnmanagedType.Bool)] public bool icon; public uint x,y; public IntPtr mask,color; }
  [StructLayout(LayoutKind.Sequential)]
  struct BITMAP { public int type,width,height,widthBytes; public ushort planes,bits; public IntPtr data; }
  public sealed class Details { public int width,height; public uint hotspotX,hotspotY; public bool isIcon; public string pixelHash; }
  public static Details Inspect(IntPtr cursor) {
    if(cursor==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(),"Cursor could not be loaded");
    ICONINFO info;
    if(!GetIconInfo(cursor,out info))throw new Win32Exception(Marshal.GetLastWin32Error());
    try {
      BITMAP bm;
      IntPtr bitmap=info.color!=IntPtr.Zero?info.color:info.mask;
      if(GetObject(bitmap,Marshal.SizeOf(typeof(BITMAP)),out bm)==0)throw new Win32Exception(Marshal.GetLastWin32Error());
      byte[] bytes=new byte[bm.widthBytes*bm.height];
      if(GetBitmapBits(bitmap,bytes.Length,bytes)!=bytes.Length)throw new Win32Exception(Marshal.GetLastWin32Error());
      string hash;
      using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
      return new Details {width=bm.width,height=info.color!=IntPtr.Zero?bm.height:bm.height/2,hotspotX=info.x,hotspotY=info.y,isIcon=info.icon,pixelHash=hash};
    } finally { if(info.mask!=IntPtr.Zero)DeleteObject(info.mask);if(info.color!=IntPtr.Zero)DeleteObject(info.color); }
  }
  public static Details InspectFile(string path) {
    IntPtr cursor=LoadCursorFromFileW(path);
    try{return Inspect(cursor);}finally{if(cursor!=IntPtr.Zero)DestroyCursor(cursor);}
  }
  public static Details InspectSystem(int id) {return Inspect(LoadCursorW(IntPtr.Zero,new IntPtr(id)));}
  public static void Reload() {
    if(!SystemParametersInfoW(0x0057,0,IntPtr.Zero,0)) {
      int error=Marshal.GetLastWin32Error();
      throw new Win32Exception(error,"Windows could not reload cursor settings; Win32 error="+error);
    }
  }
}
