namespace ExGrid.Components;

public partial class ExGrid<TRow>
{
    // Text told ahead of a Blazor event travels with its number until that handler is heard.
    // Handling takes it into the existing Input Order; voiding releases it without adoption.
    private readonly Dictionary<int, GridInputText> _inputTexts = [];
    private string? _nameInputOvertakenBy;
    private bool _nameBoxEditing;
    private long _adoptedInputTextRevision;

    private void RememberInputText(int input, GridInputText? text)
    {
        if (!_disposed && input > 0 && text is not null)
            _inputTexts[input] = text;
    }

    private GridInputText? TakeInputText(int input)
        => _inputTexts.Remove(input, out var text) ? text : null;

    private void AdoptInputText(GridInputText? text)
    {
        if (_disposed || text is null)
            return;
        // Several inputs can leave the browser before the first one's render returns. Its
        // answer may have rewritten the field (Point, F4); the same reading is not new typing.
        if (text.Revision > 0)
        {
            if (text.Revision <= _adoptedInputTextRevision)
            {
                if (text.Surface is "cell" or "bar" && string.Equals(text.Text, _editText, StringComparison.Ordinal))
                    AdoptKeyText(text.Text, text.Caret);
                return;
            }
            _adoptedInputTextRevision = text.Revision;
        }
        if (text.Surface is "cell" or "bar")
            AdoptKeyText(text.Text, text.Caret);
        else if (text.Surface == "name" && _nameBoxEditing && !string.Equals(text.Text, _nameBoxText, StringComparison.Ordinal))
        {
            _nameInputOvertakenBy = text.Text;
            _nameBoxText = text.Text;
            _suppressRender = false;
        }
    }
}
