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

        # The layer-3 (browser) tests: Node for Playwright, and dotnet because
        # Playwright starts the DemoHost itself.
        #
        # Chromium only where nixpkgs builds it. It has no aarch64-darwin
        # build, and listing it unconditionally made this shell fail to
        # EVALUATE on an Apple Silicon Mac — so `.#browser` had never once been
        # usable on the machine this project is developed on. Playwright is
        # pointed at the Chrome that is already installed (`channel: 'chrome'`),
        # which is what makes that acceptable rather than merely convenient:
        # nothing here downloads a browser.
        browser = pkgs.mkShell {
          packages = [ dotnet pkgs.nodejs ]
            ++ pkgs.lib.optional pkgs.stdenv.hostPlatform.isLinux pkgs.chromium;
          shellHook = ''
            export DOTNET_ROOT=${dotnet}/share/dotnet
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_NOLOGO=1
            echo "node $(node --version), dotnet $(dotnet --version)"
          '';
        };
      });
    };
}
