// _SharedKernel/OrderedFrameEncoder.cs — Lead owns. Phase 2.3 threaded
// JPEG farm: N encoder threads turn raw RGBA frames into JPEGs while the
// pump appends them IN ORDER (video decoders + our AVI writer require
// monotonic frames). Pure C# (NO UnityEngine), never throws out of public
// methods, never deadlocks (every wait is try + short sleep, like the
// BoundedByteQueue idiom — no Monitor.Wait chains anywhere).
//
// Striping (why no reorder map): frame seq S is owned by worker (S % N).
// Each worker has its own bounded in/out FIFOs; the pump pushes in seq
// order and drains round-robin from seq 0 upward. Order falls out of the
// topology instead of a shared sorted structure, and every queue is
// bounded so memory stays flat under any producer/consumer speed ratio.
// Shutdown: Complete() retires workers once their in-queues drain; the
// pump drains leftovers, then Dispose() joins (bounded wait, background
// threads so the runner can never hang).
using System;
using System.Collections.Generic;
using System.Threading;

public sealed class OrderedFrameEncoder : IDisposable {
  public struct FrameIn {
    public long Seq;
    public byte[] Raw;
    public int W;
    public int H;
    public int Q;
  }

  public struct FrameOut {
    public long Seq;
    public byte[] Jpeg; // null = encode refused (fatal, like the old path)
  }

  const int QueueCap = 2; // per worker fifo depth (1080p raw ~= 8 MB each)

  sealed class Lane {
    public readonly Queue<FrameIn> In = new Queue<FrameIn>();
    public readonly Queue<FrameOut> Out = new Queue<FrameOut>();
    public readonly object Gate = new object();
    public bool Done;
  }

  readonly Func<byte[], int, int, int, byte[]> _encode;
  readonly Lane[] _lanes;
  readonly Thread[] _threads;
  volatile bool _disposed;

  public int WorkerCount {
    get { return _lanes != null ? _lanes.Length : 0; }
  }

  public OrderedFrameEncoder(int workers, Func<byte[], int, int, int, byte[]> encode) {
    if (workers < 1) workers = 1;
    if (workers > 8) workers = 8;
    _encode = encode;
    _lanes = new Lane[workers];
    _threads = new Thread[workers];
    for (int i = 0; i < workers; i++) {
      _lanes[i] = new Lane();
      int idx = i;
      var t = new Thread(() => Work(idx));
      try { t.IsBackground = true; } catch (Exception) { }
      // Stay out of the gameplay thread's way: soak IDLE cores only.
      try { t.Priority = ThreadPriority.BelowNormal; } catch (Exception) { }
      try { t.Name = "MediaRecJpeg" + idx; } catch (Exception) { }
      _threads[i] = t;
      try { t.Start(); } catch (Exception) { }
    }
  }

  // Per-machine sizing (read live at session start — never baked for one
  // rig): reserve a quarter of the cores for game/render (min 1), floor 1
  // worker (a 2-core laptop then behaves exactly like the old single thread),
  // ceiling 8 (past that the memory bus, not cores, is the JPEG ceiling).
  // 2c->1, 4c->3, 8c->6, 12c->8, 64c->8. Threads run BelowNormal so gameplay
  // keeps its cores even when the farm is fully fed.
  public static int DefaultWorkerCount() {
    try {
      int cpus = Environment.ProcessorCount;
      if (cpus < 1) cpus = 2;
      int reserve = cpus / 4;
      if (reserve < 1) reserve = 1;
      int n = cpus - reserve;
      if (n < 1) n = 1;
      if (n > 8) n = 8;
      return n;
    } catch (Exception) { return 2; }
  }

  // Push one frame to its lane. Retries while the lane drains (workers run
  // until Dispose); returns false ONLY when closed/failed/disposed or the
  // lane stays wedged ~10 s (caller must treat false as FATAL and stop the
  // track — skipping a seq would wedge the round-robin drain behind the gap).
  public bool Push(long seq, byte[] raw, int w, int h, int q) {
    try {
      if (_disposed || _encode == null || raw == null) return false;
      Lane lane = _lanes[(int)(seq % _lanes.Length)];
      for (int i = 0; i < 2000; i++) {
        lock (lane.Gate) {
          if (_disposed || lane.Done) return false;
          if (lane.In.Count < QueueCap) {
            lane.In.Enqueue(new FrameIn { Seq = seq, Raw = raw, W = w, H = h, Q = q });
            return true;
          }
        }
        Thread.Sleep(5);
      }
      return false; // lane wedged: refuse rather than grow unbounded
    } catch (Exception) { return false; }
  }

  // Take the next IN-ORDER result if ready, else false (never blocks).
  public bool TryTakeOrdered(long expectSeq, out byte[] jpeg) {
    jpeg = null;
    try {
      if (_disposed) return false;
      Lane lane = _lanes[(int)(expectSeq % _lanes.Length)];
      lock (lane.Gate) {
        if (lane.Out.Count == 0) return false;
        FrameOut head = lane.Out.Peek();
        if (head.Seq != expectSeq) return false;
        lane.Out.Dequeue();
        jpeg = head.Jpeg;
        return true;
      }
    } catch (Exception) { jpeg = null; return false; }
  }

  // True when every lane is empty (pump exit predicate alongside the raw
  // queue being closed + drained).
  public bool IsIdle() {
    try {
      foreach (Lane lane in _lanes) {
        lock (lane.Gate) {
          if (lane.In.Count > 0 || lane.Out.Count > 0) return false;
        }
      }
      return true;
    } catch (Exception) { return true; }
  }

  // No more pushes: workers retire once their in-queue drains.
  public void Complete() {
    try {
      foreach (Lane lane in _lanes) {
        lock (lane.Gate) { lane.Done = true; }
      }
    } catch (Exception) { }
  }

  void Work(int idx) {
    try {
      Lane lane = _lanes[idx];
      while (true) {
        FrameIn job = new FrameIn();
        bool have = false;
        lock (lane.Gate) {
          if (_disposed) return;
          if (lane.In.Count > 0) {
            job = lane.In.Dequeue();
            have = true;
          } else if (lane.Done) {
            return;
          }
        }
        if (!have) {
          Thread.Sleep(5);
          continue;
        }
        byte[] jpeg = null;
        try {
          if (_encode != null) jpeg = _encode(job.Raw, job.W, job.H, job.Q);
        } catch (Exception) { jpeg = null; }
        // Bounded outbox (the pump drains round-robin every iteration, so
        // this park always resolves while the pump is alive; on Dispose the
        // result is dropped with the session, never wedged).
        while (true) {
          lock (lane.Gate) {
            if (_disposed) return;
            if (lane.Out.Count < QueueCap) {
              lane.Out.Enqueue(new FrameOut { Seq = job.Seq, Jpeg = jpeg });
              break;
            }
          }
          Thread.Sleep(5);
        }
      }
    } catch (Exception) { }
  }

  public void Dispose() {
    if (_disposed) return;
    _disposed = true;
    try {
      foreach (Thread t in _threads) {
        try {
          if (t != null && t.IsAlive) t.Join(5000);
        } catch (Exception) { }
      }
    } catch (Exception) { }
  }
}
