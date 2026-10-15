// A_World/PhonicsGarden/IRealPhotoAssetLoader.cs — Clean Memory-Safe Photo Loader.
// HONEST STATE (1.9.47): NO photo assets ship in this build. This loader serves REAL photos
// only when the owner registers them (future: a licensed photo pack behind Addressables);
// otherwise it reports "no photo" (null) — callers must show the honest pictogram fallback
// (PhonicsPictograms). The old version fabricated a flat-color 128x128 texture and called it a
// "real photo" — that fabrication is removed.
// Release = drop the reference (registered sprites are OWNER assets; destroying them would break
// the registration). No textures are created internally, so nothing can leak from this loader.
// C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

public interface IRealPhotoAssetLoader {
  void RegisterPhoto(string photoKey, Sprite sprite);
  bool HasPhoto(string photoKey);
  void LoadPhotoAsync(string photoKey, Action<Sprite> onLoaded);
  void ReleasePhoto(string photoKey);
  void ReleaseAll();
}

public class MemorySafePhotoLoader : IRealPhotoAssetLoader {
  readonly Dictionary<string, Sprite> _photos = new Dictionary<string, Sprite>();
  readonly HashSet<string> _activeHandles = new HashSet<string>();

  public int ActiveHandleCount => _activeHandles.Count;

  public void RegisterPhoto(string photoKey, Sprite sprite) {
    if (string.IsNullOrEmpty(photoKey)) return;
    _photos[photoKey] = sprite; // null clears the registration
  }

  public bool HasPhoto(string photoKey) {
    if (string.IsNullOrEmpty(photoKey)) return false;
    Sprite s;
    return _photos.TryGetValue(photoKey, out s) && s != null;
  }

  public void LoadPhotoAsync(string photoKey, Action<Sprite> onLoaded) {
    if (string.IsNullOrEmpty(photoKey)) {
      onLoaded?.Invoke(null);
      return;
    }

    Sprite sprite;
    if (_photos.TryGetValue(photoKey, out sprite) && sprite != null) {
      _activeHandles.Add(photoKey);
      onLoaded?.Invoke(sprite);
      return;
    }

    // Honest no-photo: never fabricate a "photo". Callers show the pictogram fallback.
    onLoaded?.Invoke(null);
  }

  public void ReleasePhoto(string photoKey) {
    if (string.IsNullOrEmpty(photoKey)) return;
    _activeHandles.Remove(photoKey); // reference dropped; owner keeps the asset itself
  }

  public void ReleaseAll() {
    _activeHandles.Clear();
  }
}
