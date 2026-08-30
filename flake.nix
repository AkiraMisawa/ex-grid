{
  description = "ex-grid — Blazor grid component: .NET toolchain (nix-managed)";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs }:
    let
      systems = [ "x86_64-linux" "aarch64-linux" "x86_64-darwin" "aarch64-darwin" ];
      forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f (import nixpkgs { inherit system; }));
    in
    {
      devShells = forAllSystems (pkgs:
        let
          # SDK 10 builds every TFM at or below itself (ADR-0022); the net8
          # runtime is included so the gating tests execute on the oldest
          # supported runtime, not just build against it.
          dotnet = pkgs.dotnetCorePackages.combinePackages [
            pkgs.dotnet-sdk_10
            pkgs.dotnetCorePackages.runtime_8_0
          ];
        in
        {
        default = pkgs.mkShell {
          packages = [ dotnet ];
          shellHook = ''
            # Child processes (the test host) resolve frameworks from DOTNET_ROOT;
            # without it they follow the muxer's real path into the SDK-only store
            # path and miss the combined net8 runtime.
            export DOTNET_ROOT=${dotnet}/share/dotnet
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
