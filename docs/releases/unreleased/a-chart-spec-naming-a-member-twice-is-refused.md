Product: ExChart
Kind: Fixed
ADR: 0320

A Chart Spec that writes a member twice, such as two `version`s, is now refused as not JSON, as a Board and a Board Input already refuse one, rather than read as whichever came last.
