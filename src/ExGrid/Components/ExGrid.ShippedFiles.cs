using System.Text.Json;
using ExGrid.Chrome;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// Each package asks for the files it was built with (ADR-0123, rewritten 2026-10-11). A browser
// that keeps an old ex-grid.min.js beside a newer ExGrid.dll runs a script that does not match the
// component: measured on a standalone application served as GitHub Pages serves one, a normal
// reload after a deploy ran the new assembly with the old script from the memory cache, and a new
// assembly that needed an export the old script lacked never attached — dimmed, busy, and silent.
//
// So the grid imports its script at an address that changes with the file's content, in every
// hosting model, and before it attaches anything it asks the module for the stamp it carries, the
// SHA-256 of its source, and compares it with the one this build was made with. A module of another
// version, one older than the stamp (it has no `stamp` to answer), and one that does not load at all
// are refused the same way: nothing is attached, the rows' place says, in the built-in Chrome's
// words, that the page holds another version's script and is to be reloaded, and the root's live
// region says it once (ADR-0033, ADR-0203). The Range Request is not taken down with the script
// (ERR-6): the Consumer is still asked for the rows. The outcome depends on what arrived, never on
// timing or on a cache (the spine's sixth principle).
//
// The stylesheet's check takes no script: the root renders a notice carrying the stamp of the
// stylesheet this build ships, hidden until a stylesheet's rules say otherwise. Each shipped
// stylesheet shows the notice for any stamp and hides it for its own, so another version's shows it
// and its own, or none yet, shows nothing.
public partial class ExGrid<TRow>
{
    /// <summary>The script's address (ADR-0123, ERR-9): the host's fingerprinted address where it
    /// knows one, else the plain one with the shipped file's content hash as <c>?v=</c>. Relative to
    /// the document's base, as Blazor resolves an import beginning <c>./</c>.</summary>
    private string ModuleAddress => "./" + PackageStylesheet.AddressOf(Assets, ShippedAssets.ScriptPath, ShippedAssets.ScriptVersion);

    /// <summary>Whether the script was refused: another version's, none, or one that broke while it
    /// attached (ADR-0123, ERR-10). Set once, by the attach. The grid then shows the refusal in place
    /// of its rows, attaches nothing, and is not busy: nothing it waits for will come.</summary>
    private bool _scriptRefused;

    /// <summary>Whether the module answers the stamp this build of the grid was made with. A module
    /// older than the stamp has no <c>stamp</c> to call, and throws, which the attach takes as the
    /// same refusal.</summary>
    private static async Task<bool> ScriptIsOwnAsync(IJSObjectReference module)
    {
        try
        {
            return await module.InvokeAsync<string?>("stamp") == ShippedAssets.ScriptStamp;
        }
        catch (JsonException)
        {
            // An answer that is not a string is no stamp of this family's.
            return false;
        }
    }

    /// <summary>The script is not this build's, or did not load: nothing stays attached, and the
    /// refusal is shown and said once, in the built-in Chrome's words (ADR-0123, ADR-0203). A handle
    /// that had attached before it broke is let go, so no listener of a script the grid refused is
    /// left on its root (ADR-0018).</summary>
    private async Task RefuseScriptAsync(IJSObjectReference? attached)
    {
        if (attached is not null)
        {
            _scrollHandle = null;
            try
            {
                await attached.InvokeVoidAsync("dispose");
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
            }
            try
            {
                await attached.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
            {
            }
        }
        if (_disposed || _scriptRefused)
            return;
        _scriptRefused = true;
        Announce(ScriptRefusedWords);
        StateHasChanged();
    }

    /// <summary>What the refusal says, worded as a refusal is (ADR-0203): a substituted Chrome's
    /// <see cref="IGridChrome.Words"/>, else the Consumer's through <see cref="CommandLabel"/>, else
    /// the built-in English.</summary>
    private string ScriptRefusedWords => RefusalWords(ReloadNoticeLabelIds.Script);

    /// <summary>What the stylesheet's notice says, should another version's stylesheet show it,
    /// worded as a refusal is (ADR-0203). The notice is in every render of the root, so the words are
    /// asked for again only when the Chrome or the Consumer's label function is another one.</summary>
    private string StylesheetNoticeWords
    {
        get
        {
            if (_stylesheetNoticeWords is null
                || !ReferenceEquals(_stylesheetNoticeLabel, CommandLabel)
                || !ReferenceEquals(_stylesheetNoticeChrome, Chrome))
            {
                _stylesheetNoticeLabel = CommandLabel;
                _stylesheetNoticeChrome = Chrome;
                _stylesheetNoticeWords = RefusalWords(ReloadNoticeLabelIds.Stylesheet);
            }
            return _stylesheetNoticeWords;
        }
    }

    private string? _stylesheetNoticeWords;
    private Func<string, string?>? _stylesheetNoticeLabel;
    private IGridChrome? _stylesheetNoticeChrome;
}
