Product: ExGrid
Kind: Fixed
ADR: 0210

Text typed while a commit's answer was out, and put into a field around the grid at its turn, is committed when the field is left or Enter is typed there, as text typed directly is. Before, a field whose owner commits on `change`, such as a Wrapper's MudBlazor text field bound with `@bind`, showed the text while its value stayed the old one. Every product drawn by ExGrid gets it, in React and in Blazor.
