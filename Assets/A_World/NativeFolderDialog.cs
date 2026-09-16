// A_World/NativeFolderDialog.cs — Agent A (World & Visual).
// Windows folder picker (SHBrowseForFolderW, new-dialog style) for choosing
// where recordings are saved. Runs on a dedicated STA thread and BLOCKS the
// caller until the user picks or cancels — the caller is always the modal
// location flow, so the game is already waiting on the user (standard file
// dialog behavior). Never throws: any failure (non-Windows, missing API,
// cancel, timeout of patience) returns false and the caller falls back to
// the default location with an honest note. EditMode/batch-safe: tests set
// SuppressForTests so no native dialog can ever open headless.
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class NativeFolderDialog {
  public static bool SuppressForTests;

  const uint BIF_RETURNONLYFSDIRS = 0x0001;
  const uint BIF_NEWDIALOGSTYLE = 0x0040;

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  struct BrowseInfo {
    public IntPtr hwndOwner;
    public IntPtr pidlRoot;
    public IntPtr pszDisplayName;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpszTitle;
    public uint ulFlags;
    public IntPtr lpfn;
    public IntPtr lParam;
    public int iImage;
  }

  [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
  static extern IntPtr SHBrowseForFolderW(ref BrowseInfo bi);

  [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
  static extern bool SHGetPathFromIDListW(IntPtr pidl,
    [MarshalAs(UnmanagedType.LPWStr)] StringBuilder path);

  [DllImport("shell32.dll")]
  static extern void CoTaskMemFree(IntPtr pv);

  [DllImport("user32.dll")]
  static extern IntPtr GetActiveWindow();

  // False = no selection (cancel / unsupported / error). Never throws,
  // never opens anything when suppressed (tests) or off Windows.
  public static bool TryPickFolder(string title, out string path) {
    path = null;
    try {
      if (SuppressForTests) return false;
      if (!IsWindows()) return false;
      string result = null;
      Exception workerError = null;
      var t = new Thread(() => {
        try { result = ShowDialog(title); }
        catch (Exception e) { workerError = e; }
      });
      try { t.SetApartmentState(ApartmentState.STA); }
      catch (Exception) { return false; }
      t.IsBackground = true;
      try { t.Start(); } catch (Exception) { return false; }
      try { t.Join(); } catch (Exception) { return false; }
      if (workerError != null || string.IsNullOrEmpty(result)) return false;
      path = result;
      return true;
    } catch (Exception) { return false; }
  }

  static bool IsWindows() {
    try {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
      return true;
#else
      return Environment.OSVersion.Platform == PlatformID.Win32NT;
#endif
    } catch (Exception) { return false; }
  }

  static string ShowDialog(string title) {
    IntPtr displayBuf = IntPtr.Zero;
    IntPtr pidl = IntPtr.Zero;
    try {
      displayBuf = Marshal.AllocHGlobal(260 * 2);
      var bi = new BrowseInfo();
      try { bi.hwndOwner = GetActiveWindow(); } catch (Exception) { bi.hwndOwner = IntPtr.Zero; }
      bi.pidlRoot = IntPtr.Zero;
      bi.pszDisplayName = displayBuf;
      bi.lpszTitle = string.IsNullOrEmpty(title) ? "Chon thu muc luu ban thu" : title;
      bi.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE;
      bi.lpfn = IntPtr.Zero;
      bi.lParam = IntPtr.Zero;
      bi.iImage = 0;
      pidl = SHBrowseForFolderW(ref bi);
      if (pidl == IntPtr.Zero) return null; // user cancelled
      var sb = new StringBuilder(260);
      if (!SHGetPathFromIDListW(pidl, sb)) return null;
      string path = sb.ToString();
      return string.IsNullOrEmpty(path) ? null : path;
    } finally {
      try { if (pidl != IntPtr.Zero) CoTaskMemFree(pidl); } catch (Exception) { }
      try { if (displayBuf != IntPtr.Zero) Marshal.FreeHGlobal(displayBuf); } catch (Exception) { }
    }
  }
}
