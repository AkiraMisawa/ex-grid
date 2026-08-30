{
  description = "cell-grid — Blazor grid component: .NET 10 toolchain (nix-managed)";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" "x86_64-darwin" "aarch64-darwin" ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f (import nixpkgs { inherit system; }));
    in
    {
      devShells = forAllSystems (pkgs: {
        default = pkgs.mkShell {
          packages = [ pkgs.dotnet-sdk_10 ];
          shellHook = ''
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_NOLOGO=1
            echo "dotnet $(dotnet --version) (nix-managed)"
          '';
        };

        # Headless browser for reproducing client-side errors in the spike.
        # Timing numbers from here are NOT representative (software rendering) —
        # real measurements must come from the user's own browser.
        browser = pkgs.mkShell {
          packages = [ pkgs.chromium pkgs.nodejs ];
          shellHook = ''
            export CHROMIUM_BIN="${pkgs.chromium}/bin/chromium"
            echo "chromium: $CHROMIUM_BIN"
          '';
        };
      });
    };
}
