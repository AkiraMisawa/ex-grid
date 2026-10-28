Product: ExSheet
Kind: Fixed
ADR: 0370

On Blazor Server, a page left while `ExSheet.Script.ExSheet` was on it could log "Unhandled exception rendering component: A task was canceled", because the closing circuit had called off the call that stops the browser's Sheet. The component now lets go quietly, as it already did once the circuit was gone.
