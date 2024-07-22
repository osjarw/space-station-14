using Content.Server.NPC.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.NPC.Components;


/// <summary>
/// WIP Implementation for http://www.gameaipro.com/GameAIPro/GameAIPro_Chapter31_Crytek's_Target_Tracks_Perception_System.pdf
/// <seealso cref=NPCTargetTracksSystem>
/// <seealso cref=NPCPerceptionSystem>
/// <summary>
[RegisterComponent]
public sealed partial class NPCTargetTracksComponent : Component
{
    /// <summary>
    /// Each entity that the NPC is aware of gets their own Track, which is a dictionary of ADSREnvelopes keyed by the
    /// type of stim.
    /// With the current implementation, only the most recent stim is kept track of, with later detections merely
    /// updating the coords and the time of the stim/envelope
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, Dictionary<StimType, ADSREnvelope>> tracks = new Dictionary<EntityUid, Dictionary<StimType, ADSREnvelope>>();

    /// <summary>
    /// Defines values for the envelopes, and if the NPC should receive the stims in the first place
    /// <summary>
    [ViewVariables]
    [DataField]
    public Dictionary<StimType, ADSREnvelopeSettings> settings = default!;
}


/// <summary>
/// The data that the NPC stores, I kinda forgot to implement a way to specify additional information, like who was
/// punched.
/// </summary>
public class ADSREnvelope
{
    [DataField]
    public EntityCoordinates Coordinates;
    [DataField]
    public TimeSpan Time;
    /// <summary>
    ///
    /// </summary>
    [DataField]
    public bool Sustained;
}

// I have no idea how yml prototypes are made
/// <summary>
/// Define how the score is calculated over time, and how long the stim exists
/// </summary>
[Prototype("adsrEnvelope")]
public class ADSREnvelopeSettings : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Ignore the stim for X seconds after being detected, possibly useful for simulating a delay in the NPC's
    /// perception, to stop them from shooting the player the millisecond they are seen.
    /// Currently not implemented
    /// </summary>
    [DataField]
    public TimeSpan AttackIgnoreTime = TimeSpan.FromSeconds(0);

    /// <summary>
    /// The score at which the stim peaks
    /// </summary>
    [DataField]
    public float AttackPeak = 1f;

    /// <summary>
    /// The score peaks after X seconds from being detected
    /// </summary>
    [DataField]
    public TimeSpan AttackTime = TimeSpan.FromSeconds(0);

    /// <summary>
    /// The score transitions from <see cref=AttackPeak> to <see cref=Sustain> over X seconds.
    /// </summary>
    [DataField]
    public TimeSpan DecayTime = TimeSpan.FromSeconds(0);

    /// <summary>
    /// The score will stay at this value if it constantly kept activated. For example, if the entity is constanty in
    /// LoS.
    /// </summary>
    [DataField]
    public float Sustain = 0.5f;

    /// <summary>
    /// The score drops from <see cref=Sustain> to 0 over X seconds, if it isn't being detected
    /// </summary>
    [DataField]
    public TimeSpan Release = TimeSpan.FromSeconds(10);

    /// <summary>
    ///
    /// </summary>
    [DataField]
    public bool Sustained = false;

    [DataField]
    public bool ResetOnRetrigger = false;

    /// <summary>
    /// The stim is forgotten after this many seconds from the initial detection,
    /// for example, if(curTime > detectionTime+TotalTime) removestim()
    /// </summary>
    public TimeSpan TotalTime => AttackIgnoreTime + AttackTime + DecayTime + Release;
    public TimeSpan SustainTime => AttackIgnoreTime + AttackTime + DecayTime;
}

public enum StimType
{
    AUDIO_MOVEMENT,
    AUDIO_COLLISION,
    AUDIO_GUN,
    VISUAL_LOS,
}

public class Stim
{
    /// <summary>
    /// Who caused this stim, who shot a gun etc.
    /// </summary>
    public EntityUid Actor;

    /// <summary>
    /// Range in which this Stim can be detected
    /// </summary>
    public float Range;

    public StimType Type;
    public EntityCoordinates Coordinates;

    /// <summary>
    /// Don't start timing out the stim if set to true, and keep score at <see cref=ADSREnvelopeSettings.Sustain>
    /// </summary>
    public bool Sustain;
}

