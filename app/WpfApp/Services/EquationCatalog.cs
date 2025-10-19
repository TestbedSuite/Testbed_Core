using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LugonTestbed.Services
{
    public sealed class EquationDef
    {
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public string description { get; set; } = "";
        public string domain { get; set; } = "";
        public Expr expr { get; set; } = new();
        public List<Param> parameters { get; set; } = new();
        public Backend backend { get; set; } = new();
        public List<string> outputs { get; set; } = new();
        public override string ToString() => name;
    }
    public sealed class Expr { public string latex { get; set; } = ""; public string infix { get; set; } = ""; }
    public sealed class Param { public string key { get; set; } = ""; public string label { get; set; } = ""; public string type { get; set; } = "string"; public object? @default { get; set; } }
    public sealed class Backend { public string kind { get; set; } = "python"; public string command { get; set; } = ""; }

    public static class EquationCatalog
    {
        private static readonly IDeserializer Yaml =
            new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

        public static List<EquationDef> Load(string root)
        {
            var eqDir = Path.Combine(root, "Equations");
            if (!Directory.Exists(eqDir)) Directory.CreateDirectory(eqDir);

            var indexPath = Path.Combine(eqDir, "equations_index.yaml");
            if (!File.Exists(indexPath))
            {
                // seed minimal examples if folder is empty
                SeedExamples(eqDir);
            }

            var index = Yaml.Deserialize<EquationIndex>(File.ReadAllText(indexPath));
            var list = new List<EquationDef>();
            foreach (var id in index.equations ?? Enumerable.Empty<string>())
            {
                var path = Path.Combine(eqDir, id + ".yaml");
                if (File.Exists(path))
                {
                    var def = Yaml.Deserialize<EquationDef>(File.ReadAllText(path));
                    if (!string.IsNullOrWhiteSpace(def.id)) list.Add(def);
                }
            }
            return list;
        }

        private sealed class EquationIndex { public List<string>? equations { get; set; } }

        private static void SeedExamples(string eqDir)
        {
            File.WriteAllText(Path.Combine(eqDir, "equations_index.yaml"),
                @"equations:
                  - poisson
                  - gw_wave
                ");
            File.WriteAllText(Path.Combine(eqDir, "poisson.yaml"),
                @"id: poisson
                name: Newtonian Poisson (placeholder)
                description: ""Solve ∇²φ = 4πGρ in weak-field limit (placeholder backend).""
                domain: gravity
                expr: { latex: ""\\nabla^2 \\phi = 4\\pi G\\rho"", infix: ""laplacian(phi) = 4*pi*G*rho"" }
                parameters:
                  - { key: gridSize, label: ""Grid Size"", type: int, default: 256 }
                  - { key: timeSteps, label: ""Time Steps"", type: int, default: 1000 }
                backend: { kind: python, command: ""python run_poisson.py --grid {gridSize} --steps {timeSteps} --out \""{outDir}\"""" }
                outputs: [ ""phi.h5"", ""run.log"" ]
                ");
            File.WriteAllText(Path.Combine(eqDir, "gw_wave.yaml"),
                @"id: gw_wave
                name: GW Propagation (toy)
                description: ""Luminal propagation with bounded phase residue (toy).""
                domain: gravity
                expr: { latex: ""\\Box h_{\\mu\\nu} = 0"", infix: ""d2(h)/dt2 - c^2*laplacian(h) = 0"" }
                parameters:
                  - { key: gridSize, label: ""Grid Size"", type: int, default: 512 }
                  - { key: timeSteps, label: ""Time Steps"", type: int, default: 2000 }
                backend: { kind: python, command: ""python run_gw.py --N {gridSize} --T {timeSteps} --out \""{outDir}\"""" }
                outputs: [ ""h.h5"", ""run.log"" ]
                ");
        }
    }
}
