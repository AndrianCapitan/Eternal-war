using Content.Shared._EternalWar.MindUpload;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._EternalWar.MindUpload;

[UsedImplicitly]
public sealed class MindUploadConsoleBoundUserInterface : BoundUserInterface
{
    private MindUploadConsoleWindow? _window;

    public MindUploadConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MindUploadConsoleWindow>();
        _window.OnUpload += () => SendMessage(new MindUploadConsoleStartMessage());
        _window.OnEjectPositronic += () => SendMessage(new MindUploadConsoleEjectPositronicMessage());
        _window.OnEjectSource += () => SendMessage(new MindUploadConsoleEjectSourceMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is MindUploadConsoleBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
