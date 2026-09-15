// _SharedKernel/MicSetupGate.cs — mic-setup gate (Lead owns).
// Pure C# (NO UnityEngine): the startup + exercise-entry decision logic for
// microphone setup. Lives in SharedKernel (not D_Audio) because the A_World
// driver (MicSetupMonitor, LWE.World) must see it and LWE.World references
// ONLY LWE.SharedKernel. The MonoBehaviour driver owns polling/UI; THIS file
// owns the state machine so EditMode tests pin it.
//
// Flow (parent-facing, Vietnamese strings live in the dialog, not here):
//   game start -> EvaluateAtStartup():
//     local mic Ready      -> ReadyLocal (play, no prompt)
//     else phone Ready     -> ReadyPhone (play, no prompt)
//     else                 -> OfferPhone ("không có microphone, dùng điện thoại không?")
//   OfferPhone + accept    -> WaitPhoneLink (instructions panel, probing)
//   OfferPhone + decline   -> Skipped (bỏ qua bài nghe; hỏi lại ở bài nghe sau)
//   WaitPhoneLink + linked -> ReadyPhone
//   WaitPhoneLink + skip   -> Skipped
//   ANY state + sources all lost (background poll) -> Skipped SILENTLY
//     (never pops UI mid-play; the next exercise entry re-offers)
//   ANY non-ready state + a source recovers (plugged/linked) -> Ready* SILENTLY
//
// Reprompt policy: NO nagging mid-play. The dialog appears only at startup
// and at listening-exercise entry (ShouldPromptAtExercise), at most once per
// exercise token (MarkPromptShown) so retries inside one exercise re-prompt
// never.
using System;

public enum MicSetupState {
  Checking,      // before EvaluateAtStartup
  ReadyLocal,    // PC mic/headset usable — play, no prompt
  OfferPhone,    // no mic anywhere — offer the phone path
  WaitPhoneLink, // user accepted — showing instructions, probing link
  ReadyPhone,    // phone link live — play, no prompt
  Skipped        // declined / unavailable — skip listening for now
}

// End-to-end phone↔gateway↔PC link observation (see PhoneLinkProbe).
public enum PhoneLinkState {
  Unknown,           // not probed yet
  GatewayDown,       // gateway/bridge unreachable (gateway chưa chạy?)
  GatewayUpNoPhone,  // gateway chạy nhưng chưa thấy điện thoại START
  PhoneLinked        // live phone session observed (AUDIO/STOP frame)
}

// Phone-link endpoint the World driver may signal. Implemented by the D_Audio
// PhoneMicrophoneDevice; the driver (LWE.World, no Audio reference) programs
// against this interface only.
public interface IPhoneLinkDevice : IMicrophoneDevice {
  void ReportLinkUp(string phoneLabel);
  void ReportLinkDown();
}

public sealed class MicSetupGate {
  readonly IMicrophoneDevice _local;
  readonly IMicrophoneDevice _phone;

  MicSetupState _state = MicSetupState.Checking;
  string _promptToken; // last exercise token the dialog was shown for (dedupe)

  public event Action<MicSetupState> StateChanged;

  public MicSetupGate(IMicrophoneDevice local, IMicrophoneDevice phone) {
    _local = local ?? throw new ArgumentNullException(nameof(local));
    _phone = phone ?? throw new ArgumentNullException(nameof(phone));
  }

  public MicSetupState State => _state;

  // True while in the phone-wait panel (driver shows instructions UI).
  public bool IsWaitingForPhone => _state == MicSetupState.WaitPhoneLink;

  // Quest/exercise policy: skip listening unless a source is Ready. Covers
  // Checking/Offer/Wait/Skipped uniformly (transient UI is not listenable).
  public bool ShouldSkipListening() {
    return _state != MicSetupState.ReadyLocal && _state != MicSetupState.ReadyPhone;
  }

  // --- startup -----------------------------------------------------------
  // Refreshes both sources, then moves out of Checking. Returns the state.
  public MicSetupState EvaluateAtStartup() {
    SafeRefresh(_local);
    try { _phone.Refresh(); } catch (Exception) { }
    return MoveTo(PickReadyState(MicSetupState.OfferPhone));
  }

  // --- user choices --------------------------------------------------------
  public void AcceptPhoneOffer() {
    if (_state == MicSetupState.OfferPhone) MoveTo(MicSetupState.WaitPhoneLink);
  }

  public void DeclinePhoneOffer() {
    if (_state == MicSetupState.OfferPhone) MoveTo(MicSetupState.Skipped);
  }

  // "Bỏ qua" inside the instructions/wait panel.
  public void SkipWaiting() {
    if (_state == MicSetupState.WaitPhoneLink) MoveTo(MicSetupState.Skipped);
  }

  // Probe result while waiting: only PhoneLinked advances (anything else
  // keeps the instructions panel open with a status line).
  public void OnPhoneLink(PhoneLinkState link) {
    if (_state == MicSetupState.WaitPhoneLink && link == PhoneLinkState.PhoneLinked)
      MoveTo(MicSetupState.ReadyPhone);
  }

  // --- background poll (silent: NEVER pops UI) -------------------------------
  // Call after Refresh()ing sources on the driver's timer. Recovery moves to
  // Ready* quietly; total loss moves to Skipped quietly (next exercise entry
  // re-offers via ShouldPromptAtExercise).
  public void NotifySourcesChanged() {
    bool localReady = SafeIsAvailable(_local);
    bool phoneReady = SafeIsAvailable(_phone);
    if (localReady && _state != MicSetupState.ReadyLocal) { MoveTo(MicSetupState.ReadyLocal); return; }
    if (!localReady && phoneReady && _state != MicSetupState.ReadyPhone) { MoveTo(MicSetupState.ReadyPhone); return; }
    if (!localReady && !phoneReady
        && _state != MicSetupState.OfferPhone
        && _state != MicSetupState.WaitPhoneLink
        && _state != MicSetupState.Skipped) {
      MoveTo(MicSetupState.Skipped);
    }
  }

  // --- exercise entry (reprompt policy: only here, once per token) ------------
  // True when the driver should SHOW the offer dialog for this exercise:
  // no source Ready, dialog not already shown for this token, and not
  // currently inside the wait panel (that panel IS the prompt).
  public bool ShouldPromptAtExercise(string exerciseToken) {
    if (!ShouldSkipListening()) return false;
    if (_state == MicSetupState.WaitPhoneLink) return false;
    if (!string.IsNullOrEmpty(exerciseToken)
        && string.Equals(exerciseToken, _promptToken, StringComparison.Ordinal))
      return false;
    return true;
  }

  public void MarkPromptShown(string exerciseToken) {
    _promptToken = exerciseToken ?? string.Empty;
    if (_state == MicSetupState.Skipped) MoveTo(MicSetupState.OfferPhone);
  }

  // --- internals -------------------------------------------------------------
  MicSetupState PickReadyState(MicSetupState whenNone) {
    if (SafeIsAvailable(_local)) return MicSetupState.ReadyLocal;
    if (SafeIsAvailable(_phone)) return MicSetupState.ReadyPhone;
    return whenNone;
  }

  MicSetupState MoveTo(MicSetupState next) {
    if (next == _state) return _state;
    _state = next;
    try { StateChanged?.Invoke(next); } catch (Exception) { }
    return _state;
  }

  static bool SafeIsAvailable(IMicrophoneDevice d) {
    try { return d.Capability.IsAvailable(); } catch (Exception) { return false; }
  }

  static void SafeRefresh(IMicrophoneDevice d) {
    try { d.Refresh(); } catch (Exception) { }
  }
}
