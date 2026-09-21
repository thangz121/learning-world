// _Bootstrap/UnitySceneOps.cs — ISceneOps production adapter (Lead owns).
// Thin SceneManager wrapper behind the WorldTransition contract: honest
// completion ONLY. AsyncOperation.completed resolves the task; a null op
// (scene missing from Build Settings) or an already-loaded query short-
// circuits without touching the state machine (it owns spam/failure rules).
// No MonoBehaviour, no state — GameInstaller owns the single instance.
// C# 9.0 only.
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class UnitySceneOps : ISceneOps {
  public bool IsLoaded(string sceneName) {
    try {
      if (string.IsNullOrEmpty(sceneName)) return false;
      Scene s = SceneManager.GetSceneByName(sceneName);
      return s.IsValid() && s.isLoaded;
    } catch (Exception) {
      return false;
    }
  }

  public Task LoadAdditiveAsync(string sceneName) {
    var tcs = new TaskCompletionSource<bool>();
    try {
      if (string.IsNullOrEmpty(sceneName)) {
        tcs.SetException(new ArgumentException("empty scene name"));
        return tcs.Task;
      }
      if (IsLoaded(sceneName)) {
        tcs.SetResult(true);
        return tcs.Task;
      }
      AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
      if (op == null) {
        tcs.SetException(new InvalidOperationException("scene not in Build Settings: " + sceneName));
        return tcs.Task;
      }
      op.completed += _ => { tcs.TrySetResult(true); };
    } catch (Exception e) {
      tcs.TrySetException(e);
    }
    return tcs.Task;
  }

  public Task UnloadAsync(string sceneName) {
    var tcs = new TaskCompletionSource<bool>();
    try {
      if (string.IsNullOrEmpty(sceneName)) {
        tcs.SetException(new ArgumentException("empty scene name"));
        return tcs.Task;
      }
      if (!IsLoaded(sceneName)) {
        tcs.SetResult(true);
        return tcs.Task;
      }
      AsyncOperation op = SceneManager.UnloadSceneAsync(sceneName);
      if (op == null) {
        tcs.SetException(new InvalidOperationException("cannot unload scene: " + sceneName));
        return tcs.Task;
      }
      op.completed += _ => { tcs.TrySetResult(true); };
    } catch (Exception e) {
      tcs.TrySetException(e);
    }
    return tcs.Task;
  }
}
