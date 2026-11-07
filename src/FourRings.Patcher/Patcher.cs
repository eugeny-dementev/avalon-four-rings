using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace FourRings;

public static class Patcher
{
    public const string TestedSha256 = "57E9FBE5659B25539FDCF1A734F0C5CDE277FEDF36EEA6FEACB237855C69EE2A";
    public const string Slot3 = "FourRings_Ring3";
    public const string Slot4 = "FourRings_Ring4";
    public static IEnumerable<string> TargetDLLs => new[] { "TG.Main.dll" };

    public static void Patch(AssemblyDefinition assembly)
    {
        using (var stream = File.OpenRead(Path.Combine(Paths.ManagedPath, "TG.Main.dll")))
        using (var sha = SHA256.Create())
        {
            var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            if (hash != TestedSha256)
                Logger.CreateLogSource("FourRings.Patcher").LogWarning("Untested Mono game build. Four Rings was tested on Mono 1.30.012; attempting initialization with structural checks enabled.");
        }
        Apply(assembly);
        Logger.CreateLogSource("FourRings.Patcher").LogInfo("Registered four ring slots; original slots 0-18 retained, additional slots 19 and 20.");
    }

    public static void Apply(AssemblyDefinition assembly)
    {
        var slot = assembly.MainModule.GetType("Awaken.TG.Main.Heroes.Items.EquipmentSlotType");
        if (slot == null) throw new InvalidOperationException("EquipmentSlotType missing.");
        if (slot.Fields.Any(f => f.Name == Slot3 || f.Name == Slot4))
        {
            if (slot.Fields.Count(f => f.Name == Slot3 || f.Name == Slot4) != 2)
                throw new InvalidOperationException("Incomplete Four Rings slot registration.");
            return;
        }
        var init = slot.Methods.Single(m => m.Name == ".cctor");
        var rules = slot.NestedTypes.Single(t => t.Name == "Rules");
        var rulesInit = rules.Methods.Single(m => m.Name == ".cctor");
        var originalRing = slot.Fields.Single(f => f.Name == "Ring2");
        var ringAssignmentEnd = init.Body.Instructions.Single(i => i.OpCode == OpCodes.Stsfld && ReferenceEquals(i.Operand, originalRing));
        var ringAssignmentStart = Previous(ringAssignmentEnd, i => i.OpCode == OpCodes.Ldstr && Equals(i.Operand, "Ring2"));
        var ringAssignment = Segment(ringAssignmentStart, ringAssignmentEnd);
        var allCall = init.Body.Instructions.Single(i => i.Operand is MethodReference m && m.Name == "AllValuesOfType");
        var ringArray = FindArray(init, "Rings", 2);
        var accessoryArray = FindArray(init, "Accessories", 4);
        var rulesArray = rulesInit.Body.Instructions.First(i => i.OpCode == OpCodes.Newarr);
        if (ReadInt(rulesArray.Previous) != 19) throw new InvalidOperationException("Unexpected native equipment rule count.");
        var ringRuleField = rulesInit.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldsfld && ((FieldReference)i.Operand).Name == "Ring2");
        var ruleEnd = ringRuleField;
        while (!(ruleEnd.OpCode == OpCodes.Newobj && ((MethodReference)ruleEnd.Operand).DeclaringType.Name == "Rules")) ruleEnd = ruleEnd.Next ?? throw new InvalidOperationException("Ring rule constructor missing.");
        var ruleTemplate = Segment(ringRuleField.Previous.Previous, ruleEnd.Next);
        var rulesStore = rulesInit.Body.Instructions.Single(i => i.OpCode == OpCodes.Stsfld && ((FieldReference)i.Operand).Name == "ByIndex");

        // Validate every required location before changing the in-memory assembly.
        if (ReadInt(ringAssignment[1]) != 11 || ruleTemplate[0].OpCode != OpCodes.Dup || ReadInt(ruleTemplate[1]) != 11)
            throw new InvalidOperationException("Unexpected ring initializer structure.");
        var added = new[] { Slot3, Slot4 }.Select(name => new FieldDefinition(name,
            FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly, slot)).ToArray();
        foreach (var field in added) slot.Fields.Add(field);
        for (int n = 0; n < added.Length; n++)
        {
            var block = ringAssignment.Select(Clone).ToArray();
            block[0].Operand = added[n].Name;
            SetInt(block[1], 19 + n);
            block[block.Length - 1].Operand = added[n];
            InsertBefore(init, allCall, block);
            var ruleBlock = ruleTemplate.Select(Clone).ToArray();
            SetInt(ruleBlock[1], 19 + n);
            ruleBlock[2].Operand = added[n];
            InsertBefore(rulesInit, rulesStore, ruleBlock);
        }
        ExtendArray(init, ringArray, added, 2);
        ExtendArray(init, accessoryArray, added, 4);
        SetInt(rulesArray.Previous, 21);
    }

    static (Instruction length, Instruction store) FindArray(MethodDefinition method, string field, int count)
    {
        var store = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Stsfld && ((FieldReference)i.Operand).Name == field);
        var allocation = Previous(store, i => i.OpCode == OpCodes.Newarr);
        if (ReadInt(allocation.Previous) != count) throw new InvalidOperationException("Unexpected " + field + " collection.");
        return (allocation.Previous, store);
    }
    static void ExtendArray(MethodDefinition method, (Instruction length, Instruction store) target, FieldDefinition[] fields, int count)
    {
        SetInt(target.length, count + fields.Length);
        for (int n = 0; n < fields.Length; n++)
            InsertBefore(method, target.store, new[] { Instruction.Create(OpCodes.Dup), Instruction.Create(OpCodes.Ldc_I4, count + n), Instruction.Create(OpCodes.Ldsfld, fields[n]), Instruction.Create(OpCodes.Stelem_Ref) });
    }
    static Instruction Previous(Instruction from, Func<Instruction, bool> predicate)
    {
        for (var i = from.Previous; i != null; i = i.Previous) if (predicate(i)) return i;
        throw new InvalidOperationException("Expected initializer not found.");
    }
    static Instruction[] Segment(Instruction start, Instruction end)
    {
        var result = new List<Instruction>();
        for (var i = start; i != null; i = i.Next) { result.Add(i); if (i == end) return result.ToArray(); }
        throw new InvalidOperationException("Unterminated initializer.");
    }
    static Instruction Clone(Instruction i)
    {
        var copy = Instruction.Create(OpCodes.Nop); copy.OpCode = i.OpCode; copy.Operand = i.Operand; return copy;
    }
    static void InsertBefore(MethodDefinition method, Instruction before, IEnumerable<Instruction> instructions)
    {
        var il = method.Body.GetILProcessor(); foreach (var i in instructions) il.InsertBefore(before, i);
    }
    static void SetInt(Instruction i, int value) { i.OpCode = OpCodes.Ldc_I4; i.Operand = value; }
    static int ReadInt(Instruction i) => i.OpCode.Code switch
    {
        Code.Ldc_I4 => (int)i.Operand, Code.Ldc_I4_S => (sbyte)i.Operand,
        Code.Ldc_I4_0 => 0, Code.Ldc_I4_1 => 1, Code.Ldc_I4_2 => 2, Code.Ldc_I4_3 => 3,
        Code.Ldc_I4_4 => 4, Code.Ldc_I4_5 => 5, Code.Ldc_I4_6 => 6, Code.Ldc_I4_7 => 7, Code.Ldc_I4_8 => 8,
        _ => throw new InvalidOperationException("Expected integer constant.")
    };
}
