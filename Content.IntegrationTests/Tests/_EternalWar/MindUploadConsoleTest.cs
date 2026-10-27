#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._EternalWar.MindUpload;
using Content.Server.Mind;
using Content.Server.Power.Components;
using Content.Shared._EternalWar.MindUpload;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._EternalWar;

[TestOf(typeof(MindUploadConsoleSystem))]
public sealed class MindUploadConsoleTest : GameTest
{
    private const string Console = "ComputerMindUpload";
    private const string Positronic = "PositronicBrain";
    private const string Pai = "PersonalAI";
    private const string Brain = "OrganHumanBrain";

    [SidedDependency(Side.Server)] private readonly MindUploadConsoleSystem _upload = default!;
    [SidedDependency(Side.Server)] private readonly ItemSlotsSystem _itemSlots = default!;
    [SidedDependency(Side.Server)] private readonly MindSystem _minds = default!;
    [SidedDependency(Side.Server)] private readonly SharedAppearanceSystem _appearance = default!;

    [Test]
    public async Task UploadsPaiAndBrainIntoPositronic()
    {
        EntityUid console = default;
        MindUploadConsoleComponent comp = null!;
        EntityUid positronic = default, pai = default;
        EntityUid paiMind = default;

        await Server.WaitPost(() =>
        {
            var uid = SSpawn(Console);
            // Spawned in nullspace there is no grid to power it, so make it work without power.
            SEntMan.RemoveComponent<ApcPowerReceiverComponent>(uid);
            console = uid;
            comp = SEntMan.GetComponent<MindUploadConsoleComponent>(uid);

            positronic = SSpawn(Positronic);
            pai = SSpawn(Pai);
            paiMind = CreateMindIn(pai);
        });

        await Server.WaitAssertion(() =>
        {
            // A positronic brain that is searching for a ghost must be rejected.
            _appearance.SetData(positronic, ToggleableGhostRoleVisuals.Status, ToggleableGhostRoleStatus.Searching);
            Assert.That(_itemSlots.TryInsert(console, comp.PositronicSlotId, positronic, null), Is.False);

            _appearance.SetData(positronic, ToggleableGhostRoleVisuals.Status, ToggleableGhostRoleStatus.Off);
            Assert.That(_itemSlots.TryInsert(console, comp.PositronicSlotId, positronic, null));

            Assert.That(_itemSlots.TryInsert(console, comp.SourceSlotId, pai, null));
            Assert.That(_upload.TryStartUpload((console, comp), null));

            // Locked while uploading.
            Assert.That(_itemSlots.TryEject(console, comp.SourceSlotId, null, out _), Is.False);
            Assert.That(_itemSlots.TryEject(console, comp.PositronicSlotId, null, out _), Is.False);
        });

        await RunSeconds(14);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_upload.HasMind(positronic), Is.False, "pAI upload finished too early");
        });

        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(comp.UploadEnd, Is.Null);
            Assert.That(SEntMan.GetComponent<MindContainerComponent>(positronic).Mind, Is.EqualTo(paiMind));

            // The pAI survives, switched off, and can be turned on again.
            Assert.That(SEntMan.Deleted(pai), Is.False);
            Assert.That(_upload.HasMind(pai), Is.False);

            Assert.That(_itemSlots.TryEject(console, comp.SourceSlotId, null, out _));
            Assert.That(_itemSlots.TryEject(console, comp.PositronicSlotId, null, out _));

            // The positronic now holds a mind and can't go back in.
            Assert.That(_itemSlots.TryInsert(console, comp.PositronicSlotId, positronic, null), Is.False);
        });

        EntityUid positronic2 = default, brain = default;
        EntityUid brainMind = default;

        await Server.WaitAssertion(() =>
        {
            positronic2 = SSpawn(Positronic);
            brain = SSpawn(Brain);
            brainMind = CreateMindIn(brain);

            // Wrong slots are refused by the whitelists.
            Assert.That(_itemSlots.TryInsert(console, comp.SourceSlotId, positronic2, null), Is.False);
            Assert.That(_itemSlots.TryInsert(console, comp.PositronicSlotId, brain, null), Is.False);

            Assert.That(_itemSlots.TryInsert(console, comp.PositronicSlotId, positronic2, null));
            Assert.That(_itemSlots.TryInsert(console, comp.SourceSlotId, brain, null));
            Assert.That(_upload.TryStartUpload((console, comp), null));
        });

        await RunSeconds(29);

        await Server.WaitAssertion(() =>
        {
            Assert.That(_upload.HasMind(positronic2), Is.False, "brain upload finished too early");
        });

        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<MindContainerComponent>(positronic2).Mind, Is.EqualTo(brainMind));
            Assert.That(SEntMan.Deleted(brain), Is.True, "the brain should be used up by the upload");
        });
    }

    [Test]
    public async Task EmptySourceDoesNotStart()
    {
        await Server.WaitAssertion(() =>
        {
            var uid = SSpawn(Console);
            SEntMan.RemoveComponent<ApcPowerReceiverComponent>(uid);
            var console = SEntity<MindUploadConsoleComponent>(uid);

            // A switched off pAI has no mind to upload.
            Assert.That(_itemSlots.TryInsert(uid, console.Comp.PositronicSlotId, SSpawn(Positronic), null));
            Assert.That(_itemSlots.TryInsert(uid, console.Comp.SourceSlotId, SSpawn(Pai), null));
            Assert.That(_upload.TryStartUpload(console, null), Is.False);
        });
    }

    private EntityUid CreateMindIn(EntityUid target)
    {
        var mind = _minds.CreateMind(null);
        _minds.TransferTo(mind.Owner, target);
        return mind.Owner;
    }
}
