using Content.Shared.Localizations;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Language of the client's own text: UI, item names and descriptions, examine text made on the client.
    /// Text made by the server, like chat and server popups, stays in the server's language.
    /// One of <see cref="ContentLocalizationManager.Languages"/>.
    /// </summary>
    public static readonly CVarDef<string> Language =
        CVarDef.Create("ew.language", ContentLocalizationManager.DefaultLanguage, CVar.ARCHIVE | CVar.CLIENTONLY);
}
