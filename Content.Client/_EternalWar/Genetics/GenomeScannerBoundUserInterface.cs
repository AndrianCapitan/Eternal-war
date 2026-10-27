using Content.Shared._EternalWar.Genetics;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._EternalWar.Genetics;

[UsedImplicitly]
public sealed class GenomeScannerBoundUserInterface : BoundUserInterface
{
    private GenomeScannerWindow? _window;

    public GenomeScannerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GenomeScannerWindow>();
        _window.OnApply += blocks => SendMessage(new GenomeScannerApplyMessage(blocks));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is GenomeScannerBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
