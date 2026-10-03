namespace ExGrid.Docs.Examples.Grid.Blotter;

/// <summary>One trade on the blotter, priced at the instrument's current quote.</summary>
public sealed record BlotterTrade(
    int Id,
    string Instrument,
    string Desk,
    string Book,
    string Trader,
    string Counterparty,
    string Side,
    string Currency,
    long Quantity,
    decimal Multiplier,
    decimal TradePrice,
    decimal PreviousClose,
    decimal Bid,
    decimal Ask,
    DateOnly TradeDate,
    string Status)
{
    /// <summary>The middle of the current bid and ask.</summary>
    public decimal Last => (Bid + Ask) / 2;

    /// <summary>The quantity with the side's sign: negative for a sale.</summary>
    public long Signed => Side == "Sell" ? -Quantity : Quantity;

    /// <summary>What the trade is worth at its trade price.</summary>
    public decimal Notional => Math.Round(TradePrice * Quantity * Multiplier, 0);

    /// <summary>The move since yesterday's close, as a fraction.</summary>
    public decimal Change => PreviousClose == 0 ? 0 : Last / PreviousClose - 1;

    /// <summary>Today's profit or loss: the move since yesterday's close.</summary>
    public decimal DayPnl => Math.Round((Last - PreviousClose) * Signed * Multiplier, 2);

    /// <summary>The profit or loss since the trade was done.</summary>
    public decimal TotalPnl => Math.Round((Last - TradePrice) * Signed * Multiplier, 2);
}
