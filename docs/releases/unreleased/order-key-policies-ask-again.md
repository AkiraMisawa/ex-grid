Product: ExPivot
Kind: Fixed
Docs: expivot/layout#an-order-key-on-a-server
ADR: 0152

Changing `OrderKeyPolicies` on a report already shown now asks for the report again, as a new layout does, so the Items are reordered by the policy the parameter names. Before, the report kept its old order until something else asked for it. The same policies handed again, in a new dictionary, ask nothing.
