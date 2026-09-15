// _Bootstrap/MicSetupBundle.cs — Lead owns. Mic-setup gate bundle (Phase 2.1).
// Additive carrier: GameInstaller builds the D_Audio mic services ONCE (the
// only place allowed to `new` services); MarketBootstrap.Build consumes the
// bundle to create the A_World dialog + monitor. Null bundle = feature off
// (all existing flows untouched).
public sealed class MicSetupBundle {
  public readonly MicSetupGate Gate;
  public readonly IMicrophoneDevice LocalMic;
  public readonly IPhoneLinkDevice PhoneMic;
  public readonly CompositeMicrophoneDevice SpeechMic; // local OR phone (future recognizer input)
  public readonly string BridgeHost;
  public readonly int BridgePort;

  public MicSetupBundle(MicSetupGate gate, IMicrophoneDevice local,
      IPhoneLinkDevice phone, CompositeMicrophoneDevice speech,
      string bridgeHost, int bridgePort) {
    Gate = gate;
    LocalMic = local;
    PhoneMic = phone;
    SpeechMic = speech;
    BridgeHost = bridgeHost;
    BridgePort = bridgePort;
  }
}
