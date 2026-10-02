// UxOsInput.cs — REAL OS-level input for the UX journey (2026-10-01).
// No InputSystem.QueueStateEvent, no WarpCursorPosition: every action goes
// through user32 SendInput against the FOREGROUND game window, exactly like a
// human mouse. Movement is closed-loop and gradual (never a teleport).
// C# 9.0 only.
using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class UxOsInput {
  [StructLayout(LayoutKind.Sequential)]
  struct POINT { public int X; public int Y; }

  [StructLayout(LayoutKind.Sequential)]
  struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

  [StructLayout(LayoutKind.Sequential)]
  struct MOUSEINPUT {
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
  }

  [StructLayout(LayoutKind.Sequential)]
  struct INPUT {
    public int type;
    public MOUSEINPUT mi;
  }

  const int INPUT_MOUSE = 0;
  const uint MOUSEEVENTF_MOVE = 0x0001;
  const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
  const uint MOUSEEVENTF_LEFTUP = 0x0004;

  [DllImport("user32.dll", SetLastError = true)]
  static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

  [DllImport("user32.dll")]
  static extern bool GetCursorPos(out POINT p);

  [DllImport("user32.dll")]
  static extern bool GetClientRect(IntPtr hWnd, out RECT r);

  [DllImport("user32.dll")]
  static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);

  [DllImport("user32.dll")]
  static extern bool SetForegroundWindow(IntPtr hWnd);

  [DllImport("user32.dll")]
  static extern bool BringWindowToTop(IntPtr hWnd);

  [DllImport("user32.dll")]
  static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

  [DllImport("user32.dll")]
  static extern IntPtr GetForegroundWindow();

  static IntPtr _hwnd;
  static int _inputSize = -1;

  public static IntPtr Handle() {
    if (_hwnd == IntPtr.Zero || !IsWindowAlive()) {
      try {
        System.Diagnostics.Process p = System.Diagnostics.Process.GetCurrentProcess();
        _hwnd = p.MainWindowHandle;
      } catch (Exception) { }
    }
    return _hwnd;
  }

  static bool IsWindowAlive() {
    try { return _hwnd != IntPtr.Zero && GetForegroundWindow() != IntPtr.Zero && _hwnd != IntPtr.Zero; }
    catch (Exception) { return false; }
  }

  // Bring the game window to the foreground (the OS delivers injected mouse
  // input to the foreground window; a background window would swallow clicks).
  public static bool Focus() {
    IntPtr h = Handle();
    if (h == IntPtr.Zero) return false;
    try {
      ShowWindow(h, 9); // SW_RESTORE
      BringWindowToTop(h);
      return SetForegroundWindow(h);
    } catch (Exception) { return false; }
  }

  public static bool IsForeground() {
    try { return Handle() != IntPtr.Zero && GetForegroundWindow() == Handle(); }
    catch (Exception) { return false; }
  }

  // Unity screen (origin bottom-left, client pixels) -> absolute screen point.
  public static Vector2 ToScreenPoint(Vector2 unityScreen) {
    IntPtr h = Handle();
    RECT cr;
    if (h == IntPtr.Zero || !GetClientRect(h, out cr)) return unityScreen;
    POINT origin = new POINT { X = 0, Y = 0 };
    ClientToScreen(h, ref origin);
    float sx = origin.X + unityScreen.x;
    float sy = origin.Y + (cr.Bottom - unityScreen.y);
    return new Vector2(sx, sy);
  }

  // Closed-loop gradual move: many small relative SendInput moves; never
  // teleports the cursor. Returns true when within tolerance.
  public static bool MoveTo(int targetX, int targetY, float tolerancePx, float stepPx, float stepDelaySec) {
    IntPtr h = Handle();
    if (h == IntPtr.Zero) return false;
    int guard = 0;
    while (guard++ < 400) {
      POINT cur;
      if (!GetCursorPos(out cur)) return false;
      float dx = targetX - cur.X;
      float dy = targetY - cur.Y;
      float dist = Mathf.Sqrt(dx * dx + dy * dy);
      if (dist <= Mathf.Max(1f, tolerancePx)) return true;
      float step = Mathf.Min(dist, Mathf.Max(1f, stepPx));
      int mx = Mathf.Clamp(Mathf.RoundToInt(dx / dist * step), -stepPx > 0 ? -(int)stepPx : int.MinValue, int.MaxValue);
      int my = Mathf.Clamp(Mathf.RoundToInt(dy / dist * step), int.MinValue, int.MaxValue);
      SendMove(mx, my);
      if (stepDelaySec > 0f) System.Threading.Thread.Sleep(Mathf.RoundToInt(stepDelaySec * 1000f));
    }
    return false;
  }

  // Current OS cursor position (absolute screen pixels).
  public static Vector2 Cursor() {
    POINT p;
    if (!GetCursorPos(out p)) return new Vector2(-1f, -1f);
    return new Vector2(p.X, p.Y);
  }

  // One incremental OS mouse move (relative). Public for a non-blocking,
  // per-frame smooth-move coroutine in the driver.
  public static void Step(int dx, int dy) {
    if (dx == 0 && dy == 0) return;
    INPUT[] arr = new INPUT[1];
    arr[0].type = INPUT_MOUSE;
    arr[0].mi.dx = dx;
    arr[0].mi.dy = dy;
    arr[0].mi.dwFlags = MOUSEEVENTF_MOVE;
    Send(arr);
  }

  static void SendMove(int dx, int dy) { Step(dx, dy); }

  public static void LeftDown() {
    INPUT[] arr = new INPUT[1];
    arr[0].type = INPUT_MOUSE;
    arr[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
    Send(arr);
  }

  public static void LeftUp() {
    INPUT[] arr = new INPUT[1];
    arr[0].type = INPUT_MOUSE;
    arr[0].mi.dwFlags = MOUSEEVENTF_LEFTUP;
    Send(arr);
  }

  static void Send(INPUT[] arr) {
    EnsureSize();
    try { SendInput((uint)arr.Length, arr, _inputSize); } catch (Exception) { }
  }

  static void EnsureSize() {
    if (_inputSize < 0) {
      try { _inputSize = Marshal.SizeOf(typeof(INPUT)); }
      catch (Exception) { _inputSize = 40; }
    }
  }
}
