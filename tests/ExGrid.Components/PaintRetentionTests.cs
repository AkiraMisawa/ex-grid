using System.Runtime.CompilerServices;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>ADR-0153: historical paint evidence must not keep an ordinary Consumer's obsolete
/// data alive through rows, column accessors, or display lookups. Exercise the component as a
/// Consumer: replace its data while it stays mounted, then collect unreachable data explicitly.
/// No waiting for GC or inspection of the grid's private history is involved.</summary>
public class PaintRetentionTests : GridTestContext
{
    [Theory] // ADR-0153 / LV-24: history retains immutable text and detached identity
    [InlineData(OwnerPath.Row)]
    [InlineData(OwnerPath.Column)]
    [InlineData(OwnerPath.PaintedText)]
    [InlineData(OwnerPath.Appearance)]
    public async Task ADR0153_an_ordinary_paints_obsolete_data_can_be_collected(OwnerPath path)
    {
        var consumer = Render<HistoryConsumer>(ps => ps.Add(c => c.Path, path));
        var owner = ObserveOwner(consumer.Instance);
        // Render buffers may keep the previous frame. Three replacements leave that frame
        // behind, while the first paint is still well inside the 64-paint gesture history.
        for (var i = 0; i < 3; i++)
            await consumer.InvokeAsync(consumer.Instance.Replace);

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        Assert.False(owner.IsAlive, $"historical paint retained the obsolete data through {path}");
        Assert.Contains("Current", consumer.Markup);
        GC.KeepAlive(consumer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ObserveOwner(HistoryConsumer consumer) => new(consumer.Owner);

    public enum OwnerPath { Row, Column, PaintedText, Appearance }

    private sealed class DataOwner
    {
        public string Text => "Current";

        public string? PaintedText(HistoryRow row, GridColumn<HistoryRow> column, double width, CellTextMetrics metrics) => Text;

        public CellAppearance Appearance(HistoryRow row, GridColumn<HistoryRow> column) => new();
    }

    private sealed record HistoryRow(int Id, DataOwner? Owner);

    private sealed class HistoryConsumer : ComponentBase
    {
        private static readonly Func<HistoryRow, object> Key = static row => row.Id;
        private static readonly ColumnWidthSpec Width = new(ColumnWidth.Fixed(100));
        private HistoryRow[] _rows = [];
        private GridColumn<HistoryRow>[] _columns = [];
        private PaintedTextOf<HistoryRow>? _paintedText;
        private CellAppearanceOf<HistoryRow>? _appearance;

        [Parameter] public OwnerPath Path { get; set; }

        public DataOwner Owner { get; private set; } = new();

        protected override void OnInitialized() => SetData();

        public void Replace()
        {
            SetData();
            StateHasChanged();
        }

        private void SetData()
        {
            var owner = Owner = new DataOwner();
            _rows = [new(1, Path == OwnerPath.Row ? owner : null)];
            Func<HistoryRow, object?> value = Path == OwnerPath.Column ? _ => owner.Text : static _ => "Current";
            _columns = [new("Value", ColumnType.Text, value, width: Width)];
            _paintedText = Path == OwnerPath.PaintedText ? owner.PaintedText : null;
            _appearance = Path == OwnerPath.Appearance ? owner.Appearance : null;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ExGrid<HistoryRow>>(0);
            builder.AddAttribute(1, nameof(ExGrid<HistoryRow>.Window), _rows);
            builder.AddAttribute(2, nameof(ExGrid<HistoryRow>.Columns), _columns);
            builder.AddAttribute(3, nameof(ExGrid<HistoryRow>.RowKey), Key);
            builder.AddAttribute(4, nameof(ExGrid<HistoryRow>.PaintedText), _paintedText);
            builder.AddAttribute(5, nameof(ExGrid<HistoryRow>.CellAppearance), _appearance);
            builder.CloseComponent();
        }
    }
}
