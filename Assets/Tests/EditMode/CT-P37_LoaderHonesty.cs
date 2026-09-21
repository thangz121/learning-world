// CT-P37: S3A loader honesty pins (Phase 3.0.x).
// WorldTransition.LastError semantics (failures carry reasons, no-op refusals
// stay silent) + on-demand architecture pin (the machine owns the single
// production load/unload call pair; Bootstrap drives it, nobody else).
// Pure contract tests with fake scene ops — no player, no scenes, no
// movement. C# 9.0 only.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

public class CT_P37_LoaderHonesty {
  sealed class FakeOps : ISceneOps {
    public readonly HashSet<string> Loaded = new HashSet<string>();
    public bool FailLoad;
    public bool FailUnload;
    public bool StickOnUnload;
    public bool IsLoaded(string s) { return Loaded.Contains(s); }
    public Task LoadAdditiveAsync(string s) {
      if (FailLoad) throw new InvalidOperationException("load fail");
      Loaded.Add(s);
      return Task.CompletedTask;
    }
    public Task UnloadAsync(string s) {
      if (FailUnload) throw new InvalidOperationException("unload fail");
      if (!StickOnUnload) Loaded.Remove(s);
      return Task.CompletedTask;
    }
  }

  static bool Run(Task<bool> t) { return t.GetAwaiter().GetResult(); }

  static string ReadRepoFile(params string[] parts) {
    var all = new List<string>();
    all.Add(Application.dataPath);
    all.AddRange(parts);
    return File.ReadAllText(Path.Combine(all.ToArray()));
  }

  // A. Failed loads carry the reason; successes leave LastError empty.
  [Test] public void P37A_LoadFailureCarriesReason() {
    var ops = new FakeOps { FailLoad = true };
    var m = new WorldTransition(SubjectIds.Main);
    Assert.IsFalse(Run(m.EnterAsync(ops, SubjectIds.Math, "MathScene")));
    Assert.IsFalse(string.IsNullOrEmpty(m.LastError), "failed load must name the reason");
    StringAssert.Contains("load failed", m.LastError);
    Assert.AreEqual(WorldTransitionState.Idle, m.State);
  }

  // B. Unverified ops (no exception, but scene state lies) carry reasons too.
  [Test] public void P37B_UnverifiedOpsCarryReasons() {
    var ops = new FakeOps();
    var m = new WorldTransition(SubjectIds.Main);
    Assert.IsTrue(Run(m.EnterAsync(ops, SubjectIds.Math, "MathScene")));
    Assert.IsTrue(string.IsNullOrEmpty(m.LastError), "clean op leaves no error");
    ops.StickOnUnload = true;
    Assert.IsFalse(Run(m.ReturnAsync(ops, "MathScene")));
    Assert.IsFalse(string.IsNullOrEmpty(m.LastError), "stuck unload must name the reason");
    StringAssert.Contains("unload unverified", m.LastError);
    Assert.AreEqual(WorldTransitionState.InSubject, m.State);
  }

  // C. No-op refusals (busy, re-enter, scene-less) are NOT errors: LastError
  // stays untouched so callers can tell "nothing to do" from "load failed".
  [Test] public void P37C_NoOpRefusalsStaySilent() {
    var ops = new FakeOps();
    var m = new WorldTransition(SubjectIds.Main);
    Assert.IsFalse(Run(m.EnterAsync(ops, SubjectIds.Thinking, "")));
    Assert.IsTrue(string.IsNullOrEmpty(m.LastError), "scene-less refusal is not an error");
    Assert.IsTrue(Run(m.EnterAsync(ops, SubjectIds.Math, "MathScene")));
    Assert.IsFalse(Run(m.EnterAsync(ops, SubjectIds.Math, "MathScene")), "re-enter refused");
    Assert.IsTrue(string.IsNullOrEmpty(m.LastError), "re-enter refusal is not an error");
  }

  // D. On-demand architecture pin: the production load/unload call pair lives
  // in exactly one place — the WorldTransition machine (driven by Bootstrap's
  // single Travel/Return path). Nobody else touches ISceneOps.
  [Test] public void P37D_SingleLoadCallSite() {
    string machine = ReadRepoFile("_SharedKernel", "WorldTransition.cs");
    Assert.AreEqual(1, new Regex(@"ops\.LoadAdditiveAsync").Matches(machine).Count,
      "machine owns the single production load call");
    Assert.AreEqual(1, new Regex(@"ops\.UnloadAsync").Matches(machine).Count,
      "machine owns the single production unload call");
    string bootstrap = ReadRepoFile("_Bootstrap", "MarketBootstrap.cs");
    Assert.IsTrue(bootstrap.Contains("TravelToSubjectAsync"),
      "Bootstrap drives entry through the single travel path");
    Assert.IsTrue(bootstrap.Contains("ReturnFromSubjectCoreAsync"),
      "Bootstrap drives return through the single return path");
    // No Math-scene loader anywhere else (firewall: no Math-specific loader).
    foreach (string[] parts in new string[][] {
      new string[] { "A_World", "MarketBuilder.cs" },
      new string[] { "A_World", "MathWorld", "MathWorldBuilder.cs" },
      new string[] { "B_Brain", "MathHostPresenter.cs" },
    }) {
      string src = ReadRepoFile(parts);
      Assert.IsFalse(src.Contains("LoadSceneAsync") || src.Contains("LoadAdditiveAsync"),
        string.Join("/", parts) + " must never load scenes (loader lives in Core)");
    }
  }
}
