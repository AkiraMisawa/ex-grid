Product: ExSheet
Kind: Fixed
ADR: 0410

A Sheet over the Script Core destroyed while its Sheet Toolbar's KeyTips stood repainted its toolbar as the band lost the keyboard, mounting a Chrome's Toolbar Item slots again on a Sheet that was gone. Under Blazor's ExSheet with a Razor Chrome, such as `MudSheetChrome`, leaving the page then threw "a product destroyed mounted a Razor slot". A destroyed Sheet's toolbar now paints nothing.
