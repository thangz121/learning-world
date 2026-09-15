// CT-P18: Source precedence — local mic/webcam > phone, per medium.
// The game decides (generic presence, names are labels only); the gateway
// relays; the phone START UI stands down. Game-side precedence applies
// regardless — the wire only moves the phone UI. Tests pin the DECISION +
// the WIRE (envelope roundtrip, strict payloads, watcher-safe ignore).
using NUnit.Framework;
using System;
using System.Text;

public class CT_P18_SourcePrecedence {
  // ---------- decision: mic ----------

  [Test] public void P18A_PreferMicFollowsLocalReady() {
    Assert.AreEqual("mic:local", PcSourcePrecedence.PreferMic(true));
    Assert.AreEqual("mic:phone", PcSourcePrecedence.PreferMic(false));
  }

  [Test] public void P18B_PreferMicForcePhoneOverrides() {
    Assert.AreEqual("mic:phone", PcSourcePrecedence.PreferMic(true, true),
      "-e2e-nomic forces phone even with local hardware");
    Assert.AreEqual("mic:phone", PcSourcePrecedence.PreferMic(false, true));
  }

  // ---------- decision: cam (generic count, no names) ----------

  [Test] public void P18C_PreferCamFollowsPresence() {
    Assert.AreEqual("cam:local", PcSourcePrecedence.PreferCam(true),
      "ANY listed webcam wins — no name checks");
    Assert.AreEqual("cam:phone", PcSourcePrecedence.PreferCam(false));
  }

  [Test] public void P18D_PreferCamForcePhoneOverrides() {
    Assert.AreEqual("cam:phone", PcSourcePrecedence.PreferCam(true, true),
      "-e2e-nocam forces phone even with a local webcam");
    Assert.AreEqual("cam:phone", PcSourcePrecedence.PreferCam(false, true));
  }

  // ---------- payload contract (strict, never throws) ----------

  [Test] public void P18E_PayloadValidation() {
    Assert.IsTrue(PcSourcePrecedence.IsPreferPayload("mic:local"));
    Assert.IsTrue(PcSourcePrecedence.IsPreferPayload("mic:phone"));
    Assert.IsTrue(PcSourcePrecedence.IsPreferPayload("cam:local"));
    Assert.IsTrue(PcSourcePrecedence.IsPreferPayload("cam:phone"));
    Assert.IsFalse(PcSourcePrecedence.IsPreferPayload(null));
    Assert.IsFalse(PcSourcePrecedence.IsPreferPayload(""));
    Assert.IsFalse(PcSourcePrecedence.IsPreferPayload("mic:local "));
    Assert.IsFalse(PcSourcePrecedence.IsPreferPayload("MIC:LOCAL"));
    Assert.IsFalse(PcSourcePrecedence.IsPreferPayload("mic:sometimes"));
    Assert.IsFalse(PcSourcePrecedence.IsPreferPayload("mic local"));
    Assert.AreEqual("mic", PcSourcePrecedence.MediaOf("mic:local"));
    Assert.AreEqual("cam", PcSourcePrecedence.MediaOf("cam:phone"));
    Assert.AreEqual("", PcSourcePrecedence.MediaOf("bogus"));
    Assert.AreEqual("", PcSourcePrecedence.MediaOf(null));
    Assert.AreEqual("local", PcSourcePrecedence.OriginOf("mic:local"));
    Assert.AreEqual("phone", PcSourcePrecedence.OriginOf("cam:phone"));
    Assert.AreEqual("", PcSourcePrecedence.OriginOf(null));
    Assert.IsTrue(PcSourcePrecedence.IsLocalOrigin("cam:local"));
    Assert.IsFalse(PcSourcePrecedence.IsLocalOrigin("cam:phone"));
    Assert.IsFalse(PcSourcePrecedence.IsLocalOrigin(null));
  }

  [Test] public void P18F_ReporterAgreesWithDecision() {
    // Transport + decision share the exact 4 payloads (no drift).
    Assert.IsTrue(PcPreferenceReporter.IsPreferPayload(PcSourcePrecedence.MicLocal));
    Assert.IsTrue(PcPreferenceReporter.IsPreferPayload(PcSourcePrecedence.MicPhone));
    Assert.IsTrue(PcPreferenceReporter.IsPreferPayload(PcSourcePrecedence.CamLocal));
    Assert.IsTrue(PcPreferenceReporter.IsPreferPayload(PcSourcePrecedence.CamPhone));
    Assert.AreEqual(PcSourcePrecedence.MicLocal, PcPreferenceReporter.MicLocal);
    Assert.AreEqual(PcSourcePrecedence.CamPhone, PcPreferenceReporter.CamPhone);
    Assert.IsFalse(PcPreferenceReporter.IsPreferPayload("mic:sometimes"));
    Assert.IsFalse(PcPreferenceReporter.IsPreferPayload(null));
    // Reporter rejects bad payloads without touching the network.
    Assert.IsFalse(PcPreferenceReporter.Report("127.0.0.1", 8451, "bogus", true));
    Assert.IsFalse(PcPreferenceReporter.Report("127.0.0.1", 8451, null, true));
    Assert.IsFalse(PcPreferenceReporter.Report("127.0.0.1", 0, "mic:local", true));
  }

  // ---------- wire: kind + envelope (both media, same numeric kind) ----------

  [Test] public void P18G_PreferKindContract() {
    Assert.AreEqual(0x12, PhoneMicProtocol.KindPreferLocal);
    Assert.AreEqual(0x12, PhoneCameraProtocol.KindPreferLocal,
      "same numeric envelope on separate sockets (§8: decode by port)");
  }

  [Test] public void P18H_MicPreferRoundTrip() {
    byte[] payload = Encoding.UTF8.GetBytes("mic:local");
    byte[] raw = PhoneMicProtocol.EncodeBridgeFrame(
      PhoneMicProtocol.KindPreferLocal, 0, 0, payload);
    PhoneMicProtocol.BridgeFrame f;
    string reason;
    Assert.IsTrue(PhoneMicProtocol.TryDecodeBridgeFrame(raw, 0, raw.Length, out f, out reason), reason);
    Assert.AreEqual(PhoneMicProtocol.KindPreferLocal, f.Kind);
    Assert.AreEqual("mic:local", PhoneMicProtocol.Utf8(f.Payload));
  }

  [Test] public void P18I_CamPreferRoundTrip() {
    byte[] payload = Encoding.UTF8.GetBytes("cam:phone");
    byte[] raw = PhoneCameraProtocol.EncodeBridgeFrame(
      PhoneCameraProtocol.KindPreferLocal, 0, 0, payload);
    PhoneCameraProtocol.BridgeFrame f;
    string reason;
    Assert.IsTrue(PhoneCameraProtocol.TryDecodeBridgeFrame(raw, 0, raw.Length, out f, out reason), reason);
    Assert.AreEqual(PhoneCameraProtocol.KindPreferLocal, f.Kind);
    Assert.AreEqual("cam:phone", PhoneCameraProtocol.Utf8(f.Payload));
  }

  [Test] public void P18J_UnknownKindStillRejected() {
    PhoneMicProtocol.BridgeFrame mf;
    string mr;
    byte[] unknownMic = PhoneMicProtocol.EncodeBridgeFrame(0x7F, 1, 1, new byte[0]);
    Assert.IsFalse(PhoneMicProtocol.TryDecodeBridgeFrame(unknownMic, 0, unknownMic.Length, out mf, out mr));
    StringAssert.StartsWith("unknown-kind", mr);
    PhoneCameraProtocol.BridgeFrame cf;
    string cr;
    byte[] unknownCam = PhoneCameraProtocol.EncodeBridgeFrame(0x7F, 1, 1, new byte[0]);
    Assert.IsFalse(PhoneCameraProtocol.TryDecodeBridgeFrame(unknownCam, 0, unknownCam.Length, out cf, out cr));
    StringAssert.StartsWith("unknown-kind", cr);
  }

  // ---------- generic rule: names never gate eligibility ----------

  [Test] public void P18K_GenericPresenceNotNames() {
    // Unknown USB/webcam/virtual names still count as usable — the prompt may
    // label them differently, but precedence follows presence only.
    Assert.IsTrue(MicDeviceClassifier.HasUsableMic(new[] { "Studio USB Mic" }));
    Assert.IsTrue(MicDeviceClassifier.HasUsableMic(new[] { "Weird Webcam 4K" }));
    Assert.AreEqual("mic:local",
      PcSourcePrecedence.PreferMic(MicDeviceClassifier.HasUsableMic(new[] { "Weird Webcam 4K" })));
    Assert.AreEqual("mic:phone",
      PcSourcePrecedence.PreferMic(MicDeviceClassifier.HasUsableMic(new string[0])));
  }

  // ---------- game-side precedence already local-wins (frozen paths) ----------

  [Test] public void P18L_CompositeLocalWinsUnchanged() {
    var local = new MicrophoneDeviceService(() => new[] { "Any Mic" }, null);
    var phone = new PhoneMicrophoneDevice();
    try { phone.ReportLinkUp("phone"); } catch (Exception) { }
    var composite = new CompositeMicrophoneDevice(local, phone);
    Assert.IsTrue(composite.Capability.IsAvailable());
    Assert.AreEqual("Any Mic", composite.SelectedDevice,
      "local wins when both Ready (precedence = existing behavior)");
    Assert.AreEqual("mic:local", PcSourcePrecedence.PreferMic(local.Capability.IsAvailable()));
  }

  [Test] public void P18M_ReporterMirrorByteEqual() {
    // Kernel mirror must stay byte-identical to the Audio source of truth
    // (same pattern as PhoneLinkProbe pin in CT-P15).
    foreach (string payload in new[] { "mic:local", "mic:phone" }) {
      byte[] mirrored = PcPreferenceReporter.EncodeMicPreferForTests(payload);
      byte[] real = PhoneMicProtocol.EncodeBridgeFrame(
        PhoneMicProtocol.KindPreferLocal, 0, 0, Encoding.UTF8.GetBytes(payload));
      CollectionAssert.AreEqual(real, mirrored, payload);
    }
    byte[] camMirrored = PhoneCameraProtocol.EncodeBridgeFrame(
      PhoneCameraProtocol.KindPreferLocal, 0, 0, Encoding.UTF8.GetBytes("cam:local"));
    PhoneCameraProtocol.BridgeFrame cf;
    string cr;
    Assert.IsTrue(PhoneCameraProtocol.TryDecodeBridgeFrame(
      camMirrored, 0, camMirrored.Length, out cf, out cr), cr);
    Assert.AreEqual("cam:local", PhoneCameraProtocol.Utf8(cf.Payload));
  }
}
