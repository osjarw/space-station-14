using Content.Server.Examine;
using Content.Server.NPC.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Collections;
using Robust.Shared.Threading;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Handles sight + sounds for NPCs.
/// </summary>
public sealed partial class NPCPerceptionSystem : EntitySystem
{
    [Dependency] private readonly NPCTargetTracksSystem _tracksSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly IParallelManager _parallel = default!;
    [Dependency] private readonly EntityLookupSystem _lookups = default!;
    [Dependency] private readonly ExamineSystem _examine = default!;


    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateRecentlyInjected(frameTime);
        LoSSensor();
    }

    /// <summary>
    /// Inform the nearby NPCs of something happening, like a gunshot
    /// </summary>
    public void CreateStim(Stim stim)
    {
        // I guess we could also have an array for each stimtype, so we wouldn't need to iterate over all NPCs
        // Also this probably could be batched somehow, like, just add stims to an array, and process them at the end of
        // an update() in parallel or something
        var query = EntityQueryEnumerator<NPCTargetTracksComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // Does the NPC listen for this stim/Does the prototype of this entity define an envelope for this type of
            // stim
            if (!comp.settings.ContainsKey(stim.Type))
                continue;

            // Is NPC in range of stim
            if (!TryComp(uid, out TransformComponent? xform))
                continue;
            if (!_transformSystem.InRange(xform.Coordinates, stim.Coordinates, stim.Range))
                continue;

            _tracksSystem.UpdateStim(uid, stim);
        }
    }

    /// <summary>
    /// Create/update LoS stims. Currently this is run every update(), and it isn't parallelized, though I think both of
    /// those are somewhat trivial to fix, all of this can probably be put in the _knowledgejob or something.
    /// </summary>
    private void LoSSensor()
    {
        var query = EntityQueryEnumerator<NPCTargetTracksComponent>();
        var mobs = new HashSet<EntityUid>();

        while (query.MoveNext(out var uid, out var comp))
        {
            // Does the NPC listen for this stim/Does the prototype of this entity define an envelope for this type of
            // stim
            if (!comp.settings.ContainsKey(StimType.VISUAL_LOS))
                continue;

            if (!TryComp(uid, out TransformComponent? xform))
                continue;

            // Use GetEntitiesInRange cause I'm too dumb to recreate what the draft used.
            mobs.Clear();
            _lookups.GetEntitiesInRange(uid, 5f, mobs);
            foreach (var mob in mobs)
            {
                if (!HasComp<MobStateComponent>(mob))
                    continue;
                if (!TryComp(mob, out TransformComponent? mobxform))
                    continue;
                if (!_examine.InRangeUnOccluded(uid, mob, 5f))
                    continue;

                _tracksSystem.UpdateStim(uid,
                        new Stim()
                        {
                            Actor = mob,
                            Coordinates = mobxform.Coordinates,
                            Type = StimType.VISUAL_LOS
                            // Sustain = true,
                        });
            }

        }

    }
}
