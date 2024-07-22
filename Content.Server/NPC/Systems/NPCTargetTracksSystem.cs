using System.Diagnostics.CodeAnalysis;
using Content.Server.NPC.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.NPC.Systems;

// This system probably needs more/better queries for retrieving information
public sealed partial class NPCTargetTracksSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Update(float frameTime)
    {
        // Remove stims that have expired. We could probably do this with a priority queue like how it's described in
        // gameaipro, http://www.gameaipro.com/GameAIProOnlineEdition2021/GameAIProOnlineEdition2021_Chapter02_Efficient_Event_Based_Simulations.pdf
        // but the current way of resetting time might not work with a priority queue
        var query = EntityQueryEnumerator<NPCTargetTracksComponent>();
        var curTime = _gameTiming.CurTime;
        while (query.MoveNext(out var uid, out var entity))
        {
            foreach (var (target, track) in entity.tracks)
            {
                foreach (var (stim, adsr) in track)
                {
                    Logger.Debug("{uid} {who} {type} {score}", uid, target, stim, GetScore(uid, target, stim));
                    // Remove stim/envelope if it isn't sustained, and the stim is older than TotalTime
                    if (!adsr.Sustained && adsr.Time + entity.settings[stim].TotalTime < curTime)
                        track.Remove(stim);
                }
            }

        }
    }

    public float GetScore(EntityUid uid, EntityUid target, StimType type)
    {
        if (!TryComp<NPCTargetTracksComponent>(uid, out var comp))
            return 0f;

        if (!TryGetTrack(uid, target, out var track))
            return 0f;

        if (!track.TryGetValue(type, out var envelope))
            return 0f;

        var settings = comp.settings[type];
        var curTime = _gameTiming.CurTime;
        var attackTime = envelope.Time + settings.AttackTime;
        var decayTime = attackTime + settings.DecayTime;

        // If the stim is being constantly activated, such as by having LoS, then return sustain value
        if(envelope.Sustained && curTime > decayTime)
            return settings.Sustain;

        // I'm too dumb to figure out a better way to implement the ADSR envelope
        if (attackTime > curTime)
            return (float)MathHelper.Lerp(0f, settings.AttackPeak, (curTime - envelope.Time) / (settings.AttackTime));
        if (decayTime > curTime)
            return (float)MathHelper.Lerp(settings.Sustain, settings.AttackPeak, 1 - (curTime - attackTime) / (settings.DecayTime));
        return (float)MathHelper.Lerp(0f, settings.Sustain, 1 - (curTime - decayTime) / (settings.Release));

    }

    public void UpdateStim(EntityUid uid, Stim stim)
    {
        if (!TryComp<NPCTargetTracksComponent>(uid, out var comp))
            throw new Exception("No target track component found");
        if (!TryGetTrack(uid, stim.Actor, out var track))
            return;

        if (!track.TryGetValue(stim.Type, out var envelope))
        {
            track.Add(stim.Type, new ADSREnvelope() { Coordinates = stim.Coordinates, Time = _gameTiming.CurTime, Sustained=stim.Sustain});
        }
        else
        {
            track[stim.Type].Coordinates = stim.Coordinates;
            track[stim.Type].Sustained = stim.Sustain;

            if (comp.settings[stim.Type].ResetOnRetrigger)
                track[stim.Type].Time = _gameTiming.CurTime;

            // Move time to start of sustain if we're already releasing
            if(_gameTiming.CurTime > track[stim.Type].Time+comp.settings[stim.Type].SustainTime)
                track[stim.Type].Time = _gameTiming.CurTime-comp.settings[stim.Type].SustainTime;
        }

    }

    public bool TryGetTrack(EntityUid uid, EntityUid stimulant, [NotNullWhen(true)] out Dictionary<StimType, ADSREnvelope>? track)
    {
        track = null;
        if (!TryComp<NPCTargetTracksComponent>(uid, out var comp))
            return false;

        if (!comp.tracks.TryGetValue(stimulant, out var tracks))
        {
            tracks = new Dictionary<StimType, ADSREnvelope>();
            comp.tracks.Add(stimulant, tracks);
        }

        track = tracks;
        return true;
    }

}
