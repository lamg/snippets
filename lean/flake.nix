{
  description = "Lean rewrite of the Snippets LSP server";

  inputs = {
    nixpkgs.url = "flake:nixpkgs";
  };

  outputs = { self, nixpkgs }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
    in
    {
      devShells.${system}.default = pkgs.mkShell {
        packages = [ pkgs.elan ];
        shellHook = ''
          echo "Lean toolchain: $(lean --version)"
        '';
      };
    };
}
