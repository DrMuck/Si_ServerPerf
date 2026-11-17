// TargetCheck: static check of every string-named Harmony target / reflected member of Si_ServerPerf against a game
// build, with Mono.Cecil (no game process). refcheck covers compile-time references; this covers what refcheck cannot
// see (AccessTools by name, private members, exact parameter lists).
//
//   dotnet run --project tools\TargetCheck -c Release -- <server_Managed dir> <Si_ServerPerf dir>
//
// Reads Core\targets.txt and Modules\*\targets.txt. One entry per line ('#' = comment). Type names are Cecil full
// names (namespace.Type, nested Outer/Inner); prefix "Asm:" picks another assembly of the managed dir
// (default SilicaCore), e.g. "AstarPathfindingProject:Pathfinding.NavmeshUpdates".
//   M|Type|Method|ParamFullName,ParamFullName|declaredOnly(0/1)    method; empty param list = no parameters
//   F|Type|Field|FieldTypeFullName                                field (instance or static)
//   P|Type|Property                                               property with a getter
// Exit code = number of unresolved entries. Small non-virtual targets (< 20 bytes IL) are flagged: Mono's JIT may
// inline them into callers, where a Harmony patch then does not run.
using Mono.Cecil;

var managed = args[0];
var project = args[1];
var res = new DefaultAssemblyResolver();
res.AddSearchDirectory(managed);
var modules = new Dictionary<string, ModuleDefinition>(StringComparer.OrdinalIgnoreCase);
ModuleDefinition Asm(string name)
{
    if (!modules.TryGetValue(name, out var m))
    {
        string path = Path.Combine(managed, name + ".dll");
        m = File.Exists(path) ? ModuleDefinition.ReadModule(path, new ReaderParameters { AssemblyResolver = res }) : null;
        modules[name] = m;
    }
    return m;
}

var files = new List<string>();
if (File.Exists(Path.Combine(project, "Core", "targets.txt"))) files.Add(Path.Combine(project, "Core", "targets.txt"));
if (Directory.Exists(Path.Combine(project, "Modules")))
    files.AddRange(Directory.GetFiles(Path.Combine(project, "Modules"), "targets.txt", SearchOption.AllDirectories).OrderBy(x => x));

int bad = 0, ok = 0;
foreach (var file in files)
{
    Console.WriteLine("== " + Path.GetRelativePath(project, file));
    foreach (var raw in File.ReadAllLines(file))
    {
        var l = raw.Trim();
        if (l.Length == 0 || l.StartsWith("#")) { if (l.StartsWith("# ")) Console.WriteLine(l); continue; }
        var p = l.Split('|');
        string asmName = "SilicaCore", typeName = p[1];
        int colon = typeName.IndexOf(':');
        if (colon > 0) { asmName = typeName.Substring(0, colon); typeName = typeName.Substring(colon + 1); }
        var mod = Asm(asmName);
        var td = mod?.GetTypes().FirstOrDefault(t => t.FullName == typeName);
        if (td == null) { Console.WriteLine("  MISSING TYPE   " + l); bad++; continue; }
        if (p[0] == "M")
        {
            var want = p[3].Length == 0 ? new string[0] : p[3].Split(',');
            bool declared = p.Length > 4 && p[4] == "1";
            MethodDefinition hit = null; string where = null;
            for (var t = td; t != null; t = declared ? null : t.BaseType?.Resolve())
            {
                hit = t.Methods.FirstOrDefault(m => m.Name == p[2] && m.Parameters.Select(x => x.ParameterType.FullName).SequenceEqual(want));
                if (hit != null) { where = t.FullName; break; }
            }
            if (hit == null) { Console.WriteLine("  MISSING METHOD " + l); bad++; continue; }
            int size = hit.HasBody ? hit.Body.CodeSize : -1;
            string flags = (hit.IsStatic ? "static " : "") + (hit.IsVirtual ? "virtual " : "") + (hit.IsPublic ? "public" : hit.IsPrivate ? "private" : "nonpublic");
            string inl = (!hit.IsVirtual && size >= 0 && size < 20) ? "  <-- small non-virtual: Mono may inline it at call sites" : "";
            Console.WriteLine($"  OK  {where}::{hit.Name}({string.Join(",", want)}) -> {hit.ReturnType.Name} [{flags}] IL {size} B{inl}");
        }
        else if (p[0] == "F")
        {
            var f = td.Fields.FirstOrDefault(x => x.Name == p[2]);
            if (f == null || f.FieldType.FullName != p[3]) { Console.WriteLine("  MISSING FIELD  " + l + (f != null ? " (type is " + f.FieldType.FullName + ")" : "")); bad++; continue; }
            Console.WriteLine($"  OK  {td.FullName}.{f.Name} : {f.FieldType.FullName} [{(f.IsStatic ? "static " : "")}{(f.IsPublic ? "public" : "nonpublic")}]");
        }
        else if (p[0] == "P")
        {
            var pr = td.Properties.FirstOrDefault(x => x.Name == p[2]);
            if (pr == null || pr.GetMethod == null) { Console.WriteLine("  MISSING PROP   " + l); bad++; continue; }
            Console.WriteLine($"  OK  {td.FullName}.{pr.Name} : {pr.PropertyType.FullName} (getter)");
        }
        else { Console.WriteLine("  BAD LINE       " + l); bad++; continue; }
        ok++;
    }
}
Console.WriteLine(bad == 0 ? "ALL " + ok + " RESOLVED" : bad + " UNRESOLVED, " + ok + " resolved");
return bad;
