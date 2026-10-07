using System.Runtime.CompilerServices;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>ADR-0154: Action address evidence must not keep an ordinary Consumer's obsolete
/// data alive through rows, column accessors, or display lookups. Exercise the component as a
/// Consumer: replace its data while it stays mounted, then collect unreachable data explicitly.
/// No waiting for GC or inspection of the grid's private history is involved.</summary>
public class PaintRetentionTests : GridTestContext
{
    [Theory] // ADR-0154 / LV-24: only detached Action identities may outlive a render
    [InlineData(OwnerPath.Row, false)]
    [InlineData(OwnerPath.Row, true)]
    [InlineData(OwnerPath.Text, false)]
    [InlineData(OwnerPath.Text, true)]
    [InlineData(OwnerPath.Column, false)]
    [InlineData(OwnerPath.Column, true)]
    [InlineData(OwnerPath.PaintedText, false)]
    [InlineData(OwnerPath.PaintedText, true)]
    [InlineData(OwnerPath.Appearance, false)]
    [InlineData(OwnerPath.Appearance, true)]
    [InlineData(OwnerPath.RowKeyDelegate, false)]
    [InlineData(OwnerPath.RowKeyDelegate, true)]
    [InlineData(OwnerPath.Source, false)]
    [InlineData(OwnerPath.Source, true)]
    public async Task ADR0154_obsolete_data_can_be_collected_with_or_without_actions(OwnerPath path, bool actions)
    {
        var consumer = Render<HistoryConsumer>(ps => ps.Add(c => c.Path, path).Add(c => c.Actions, actions));
        var owner = ObserveOwner(consumer.Instance);
        // Render buffers may keep the previous frame. Three replacements leave that frame
        // behind. With Actions, the first address remains inside their bounded history.
        for (var i = 0; i < 3; i++)
            await consumer.InvokeAsync(consumer.Instance.Replace);

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

        Assert.False(owner.IsAlive, $"historical paint retained the obsolete data through {path}");
        Assert.Contains("Current", consumer.Markup);
        GC.KeepAlive(consumer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ObserveOwner(HistoryConsumer consumer) => new(consumer.Path == OwnerPath.Text ? consumer.Owner.Text : consumer.Owner);

    public enum OwnerPath { Text, Row, Column, PaintedText, Appearance, RowKeyDelegate, Source }

    private sealed class DataOwner
    {
        public string Text { get; } = "Current " + Guid.NewGuid().ToString();

        public string? PaintedText(HistoryRow row, GridColumn<HistoryRow> column, double width, CellTextMetrics metrics) => Text;

        public CellAppearance Appearance(HistoryRow row, GridColumn<HistoryRow> column) => new();
    }

    private sealed record HistoryRow(int Id, DataOwner? Owner);

    private sealed class HistoryConsumer : ComponentBase
    {
        private Func<HistoryRow, object> _key = static row => row.Id;
        private static readonly ColumnWidthSpec Width = new(ColumnWidth.Fixed(100));
        private HistoryRow[] _rows = [];
        private GridColumn<HistoryRow>[] _columns = [];
        private PaintedTextOf<HistoryRow>? _paintedText;
        private CellAppearanceOf<HistoryRow>? _appearance;
        private IGridSource<HistoryRow>? _source;

        [Parameter] public OwnerPath Path { get; set; }
        [Parameter] public bool Actions { get; set; }

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
            _rows = [new(1, Path is OwnerPath.Row or OwnerPath.Text ? owner : null)];
            Func<HistoryRow, object?> value = Path == OwnerPath.Column ? _ => owner.Text
                : Path == OwnerPath.Text ? static row => row.Owner!.Text : static _ => "Current";
            _columns = Actions
                ? [new("Value", ColumnType.Text, value, width: Width),
                    GridColumn<HistoryRow>.ActionColumn("Do", [new GridAction("approve", "Approve")], width: Width)]
                : [new("Value", ColumnType.Text, value, width: Width)];
            _key = Path == OwnerPath.RowKeyDelegate ? row => { GC.KeepAlive(owner); return row.Id; } : static row => row.Id;
            _paintedText = Path == OwnerPath.PaintedText ? owner.PaintedText : null;
            _appearance = Path == OwnerPath.Appearance ? owner.Appearance : null;
            _source = Path == OwnerPath.Source
                ? GridSource.From(_rows, row => { GC.KeepAlive(owner); return row.Id; }) : null;
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ExGrid<HistoryRow>>(0);
            if (_source is { } source) builder.AddAttribute(1, nameof(ExGrid<HistoryRow>.Source), source);
            else builder.AddAttribute(2, nameof(ExGrid<HistoryRow>.Window), _rows);
            builder.AddAttribute(3, nameof(ExGrid<HistoryRow>.Columns), _columns);
            builder.AddAttribute(4, nameof(ExGrid<HistoryRow>.RowKey), _key);
            builder.AddAttribute(5, nameof(ExGrid<HistoryRow>.PaintedText), _paintedText);
            builder.AddAttribute(6, nameof(ExGrid<HistoryRow>.CellAppearance), _appearance);
            builder.CloseComponent();
        }
    }
}
