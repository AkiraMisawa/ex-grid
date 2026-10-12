using ExGrid.Chrome;

namespace ExSheet.Components;

// ExSheet's stylesheet notice (ADR-0123, ERR-11): the Sheet's root carries the stamp of the
// stylesheet this build of ExSheet ships, hidden until a stylesheet's rules decide. Every shipped
// ex-sheet.min.css shows it for any stamp and hides it for its own, so another version's shows it,
// this version's hides it, and none yet loaded shows nothing, with no script. The grid inside the
// Sheet carries ExGrid's own.
public partial class ExSheet
{
    /// <summary>The name the stylesheet's notice rules find this notice by.</summary>
    private static string NoticeStylesheet => ShippedAssets.StylesheetName;

    /// <summary>The SHA-256 of the stylesheet's source this build ships: its stamp.</summary>
    private static string NoticeStamp => ShippedAssets.StylesheetStamp;

    /// <summary>What the notice says, through the Consumer's label function and the built-in
    /// Chrome's English where it says nothing (ADR-0203), asked again only when the function is
    /// another one.</summary>
    private string StylesheetNoticeWords
    {
        get
        {
            if (_stylesheetNoticeWords is null || !ReferenceEquals(_stylesheetNoticeLabel, CommandLabel))
            {
                _stylesheetNoticeLabel = CommandLabel;
                _stylesheetNoticeWords = CommandLabel?.Invoke(ReloadNoticeLabelIds.Stylesheet)
                    ?? BuiltInCommandLabels.For(ReloadNoticeLabelIds.Stylesheet);
            }
            return _stylesheetNoticeWords;
        }
    }

    private string? _stylesheetNoticeWords;
    private Func<string, string?>? _stylesheetNoticeLabel;
}
