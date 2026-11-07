using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FourRings;
using Mono.Cecil;
using Mono.Cecil.Cil;

Check(args.Length == 3, "game assembly, plugin assembly, and expected harness state supplied");
if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[0]))) != Patcher.TestedSha256)
    Console.WriteLine("WARNING: untested Mono game fingerprint; running structural checks.");
using (var plugin = AssemblyDefinition.ReadAssembly(args[1]))
{
    Check(plugin.MainModule.Types.Any(t => t.Name == "TestHarness") == bool.Parse(args[2]), "plugin harness state matches build request");
}
using var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
using var assembly = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver });
var slots = assembly.MainModule.GetType("Awaken.TG.Main.Heroes.Items.EquipmentSlotType");
var initial = slots.Methods.Single(m => m.Name == ".cctor");
var originalIndexes = Indexes(initial);
Check(originalIndexes.Values.OrderBy(x => x).SequenceEqual(Enumerable.Range(0, 19)), "native indexes are exactly 0 through 18");
var originalAssignments = initial.Body.Instructions.Where(i => i.OpCode == OpCodes.Stsfld && ((FieldReference)i.Operand).FieldType.FullName == slots.FullName).Select(i => i.ToString()).ToArray();
Patcher.Apply(assembly);
Check(slots.Fields.Count(f => f.Name.StartsWith("FourRings_")) == 2, "two new fields");
Check(originalAssignments.All(s => initial.Body.Instructions.Any(i => i.ToString() == s)), "existing slot assignments retained");
var patchedIndexes = Indexes(initial);
Check(originalIndexes.All(p => patchedIndexes[p.Key] == p.Value), "every original index retained");
Check(patchedIndexes[Patcher.Slot3] == 19 && patchedIndexes[Patcher.Slot4] == 20 && patchedIndexes.Values.Distinct().Count() == 21, "new indexes are unique and stable");
foreach (var name in new[] { Patcher.Slot3, Patcher.Slot4 })
{
    var f = slots.Fields.Single(f => f.Name == name);
    Check(f.IsPublic && f.IsStatic && f.IsInitOnly, "stable public readonly slot " + name);
    Check(initial.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && Equals(i.Operand, name)), "serialized name " + name);
}
var total = initial.Body.Instructions.Count;
Patcher.Apply(assembly);
Check(initial.Body.Instructions.Count == total, "applying twice does not duplicate IL");
var rules = slots.NestedTypes.Single(t => t.Name == "Rules").Methods.Single(m => m.Name == ".cctor");
Check((int)rules.Body.Instructions.First(i => i.OpCode == OpCodes.Newarr).Previous.Operand == 21, "21 acceptance rules");
foreach (var name in new[] { Patcher.Slot3, Patcher.Slot4 })
{
    var cursor = rules.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldsfld && ((FieldReference)i.Operand).Name == name).Next;
    var accepted = new System.Collections.Generic.List<string>();
    while (cursor.OpCode != OpCodes.Newobj)
    {
        if (cursor.OpCode == OpCodes.Ldsfld) accepted.Add(((FieldReference)cursor.Operand).Name);
        cursor = cursor.Next;
    }
    Check(accepted.SequenceEqual(new[] { "Ring" }), name + " accepts only rings");
}
foreach (var field in new[] { "Rings", "Accessories" })
{
    var store = initial.Body.Instructions.Single(i => i.OpCode == OpCodes.Stsfld && ((FieldReference)i.Operand).Name == field);
    var cursor = store.Previous;
    while (cursor.OpCode != OpCodes.Newarr) cursor = cursor.Previous;
    Check((int)cursor.Previous.Operand == (field == "Rings" ? 4 : 6), "expanded " + field);
}
using var bytes = new MemoryStream();
assembly.Write(bytes);
bytes.Position = 0;
using var roundTrip = AssemblyDefinition.ReadAssembly(bytes);
Check(roundTrip.MainModule.GetType(slots.FullName).Fields.Any(f => f.Name == Patcher.Slot4), "modified assembly writes and reads");

// Exercise the real startup entry point against a different fingerprint without changing the installed game.
var fixtureDirectory = Path.Combine(Path.GetTempPath(), "FourRings.Static-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureDirectory);
var managedPath = typeof(BepInEx.Paths).GetProperty("ManagedPath")!;
var originalManagedPath = managedPath.GetValue(null);
try
{
    var fixturePath = Path.Combine(fixtureDirectory, "TG.Main.dll");
    using (var fixture = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver }))
    {
        fixture.MainModule.Mvid = Guid.NewGuid();
        fixture.Write(fixturePath);
    }
    Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixturePath))) != Patcher.TestedSha256, "fixture has an untested fingerprint");
    managedPath.SetValue(null, fixtureDirectory);
    using var fixtureAssembly = AssemblyDefinition.ReadAssembly(fixturePath, new ReaderParameters { AssemblyResolver = resolver });
    Patcher.Patch(fixtureAssembly);
    Check(fixtureAssembly.MainModule.GetType(slots.FullName).Fields.Any(f => f.Name == Patcher.Slot4), "startup permits an untested fingerprint with compatible structures");
}
finally
{
    managedPath.SetValue(null, originalManagedPath);
    Directory.Delete(fixtureDirectory, true);
}

RejectChangedStructure("ring collection count", slot =>
{
    var method = slot.Methods.Single(m => m.Name == ".cctor");
    var cursor = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Stsfld && ((FieldReference)i.Operand).Name == "Rings");
    while (cursor.OpCode != OpCodes.Newarr) cursor = cursor.Previous;
    cursor.Previous.OpCode = OpCodes.Ldc_I4;
    cursor.Previous.Operand = 3;
});
RejectChangedStructure("native rule count", slot =>
{
    var method = slot.NestedTypes.Single(t => t.Name == "Rules").Methods.Single(m => m.Name == ".cctor");
    var count = method.Body.Instructions.First(i => i.OpCode == OpCodes.Newarr).Previous;
    count.OpCode = OpCodes.Ldc_I4;
    count.Operand = 20;
});
RejectChangedStructure("missing Ring2 field", slot => slot.Fields.Single(f => f.Name == "Ring2").Name = "ChangedRing2");
RejectChangedStructure("incomplete slot registration", slot =>
    slot.Fields.Add(new FieldDefinition(Patcher.Slot3, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly, slot)));
Console.WriteLine("PASS: all static patch checks");
void RejectChangedStructure(string label, Action<TypeDefinition> mutate)
{
    using var fixture = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { AssemblyResolver = resolver });
    var slot = fixture.MainModule.GetType(slots.FullName);
    mutate(slot);
    using var before = new MemoryStream();
    fixture.Write(before);
    bool rejected = false;
    try { Patcher.Apply(fixture); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "reject incompatible " + label);
    using var after = new MemoryStream();
    fixture.Write(after);
    Check(before.ToArray().SequenceEqual(after.ToArray()), "rejection leaves assembly unchanged: " + label);
}
static void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); Console.WriteLine("PASS: " + label); }
static System.Collections.Generic.Dictionary<string, int> Indexes(MethodDefinition init)
{
    return init.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr && i.Next.OpCode.Code.ToString().StartsWith("Ldc_I4"))
        .ToDictionary(i => (string)i.Operand, i => i.Next.OpCode.Code switch
        {
            Code.Ldc_I4 => (int)i.Next.Operand, Code.Ldc_I4_S => (sbyte)i.Next.Operand,
            Code.Ldc_I4_0 => 0, Code.Ldc_I4_1 => 1, Code.Ldc_I4_2 => 2, Code.Ldc_I4_3 => 3,
            Code.Ldc_I4_4 => 4, Code.Ldc_I4_5 => 5, Code.Ldc_I4_6 => 6, Code.Ldc_I4_7 => 7, Code.Ldc_I4_8 => 8,
            _ => throw new Exception("Unexpected index opcode")
        });
}
