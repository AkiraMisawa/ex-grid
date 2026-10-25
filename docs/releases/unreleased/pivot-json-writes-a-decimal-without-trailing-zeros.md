Product: ExPivot
Kind: Behaviour
ADR: 0064

`PivotJson` writes an exact number without trailing zeros, since a Decimal is a value and not the places it was written with: a sum of `100.50` crosses as `100.5`, the same value. A document that is not JSON is refused with the sentence `A Pivot Source document is JSON.`, the parser's message kept as the inner exception, and a model refusal it wraps no longer ends with .NET's parameter suffix. A server and a browser built on either engine now write one document as the same text.
