using System;
using System.Linq;
using System.Reflection;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Equipment;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Utility.RichEnums;
using Awaken.TG.MVC;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FourRings;

[BepInPlugin("local.avalon.fourrings", "Four Rings", "1.0.0")]
[BepInDependency("Better_UI", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    internal static readonly FieldInfo SlotType = AccessTools.Field(typeof(VCDefaultEquipmentSlot), "type");
    internal static readonly FieldInfo Attached = AccessTools.Field(typeof(ViewComponent), "_isAttached");
    internal static readonly FieldInfo Navigation = AccessTools.Field(typeof(VCEquipmentSlotBase), "navigation");

    void Awake()
    {
        Log = Logger;
        if (EquipmentSlotType.All.Length != 21 || EquipmentSlotType.Rings.Length != 4 ||
            EquipmentSlotType.All[19].EnumName != "FourRings_Ring3" || EquipmentSlotType.All[20].EnumName != "FourRings_Ring4")
            throw new InvalidOperationException("Four Rings startup patcher is missing or incompatible. Do not load a save that uses extra slots.");
        new Harmony("local.avalon.fourrings").PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo("Four Rings ready: four native ring slots, including save identifiers and acceptance rules.");
    }

    [HarmonyPatch(typeof(VEquipmentUI), "OnInitialize")]
    static class EquipmentViewPatch
    {
        [HarmonyPostfix]
        [HarmonyAfter("Better_UI")]
        static void Postfix(VEquipmentUI __instance)
        {
            if (__instance.GetComponentsInChildren<VCDefaultEquipmentSlot>(true).Any(s => s.Type == EquipmentSlotType.All[19])) return;
            var slots = __instance.GetComponentsInChildren<VCDefaultEquipmentSlot>(true);
            var originals = new[] { slots.Single(s => s.Type == EquipmentSlotType.Ring1), slots.Single(s => s.Type == EquipmentSlotType.Ring2) };
            var boots = slots.Single(s => s.Type == EquipmentSlotType.Boots);
            for (int n = 0; n < originals.Length; n++)
            {
                var source = originals[n];
                var cloneObject = Instantiate(source.gameObject, source.transform.parent, false);
                cloneObject.name = "FourRings_Ring" + (n + 3);
                var clone = cloneObject.GetComponent<VCDefaultEquipmentSlot>();
                SlotType.SetValue(clone, new RichEnumReference(EquipmentSlotType.All[19 + n]));
                foreach (var component in cloneObject.GetComponentsInChildren<ViewComponent>(true)) Attached.SetValue(component, false);
                (cloneObject.GetComponent<LayoutElement>() ?? cloneObject.AddComponent<LayoutElement>()).ignoreLayout = true;
                var alignment = cloneObject.AddComponent<RingAlignment>();
                alignment.Source = source.transform;
                alignment.Boots = boots.transform;
                SetNeighbor(source, "down", cloneObject);
                SetNeighbor(clone, "up", source.gameObject);
                SetNeighbor(clone, "down", null);
                SetNeighbor(clone, n == 0 ? "right" : "left", boots.gameObject);
                SetNeighbor(boots, n == 0 ? "left" : "right", cloneObject);
            }
            Log.LogInfo("Added two ring controls to the equipment diagram.");
        }
    }

    // Follow the existing diagram during its chooser animations without changing its layout groups.
    public sealed class RingAlignment : MonoBehaviour
    {
        public Transform Source = null!;
        public Transform Boots = null!;
        void LateUpdate()
        {
            var position = new Vector3(Source.position.x, Boots.position.y, Source.position.z);
            if (transform.position != position) transform.position = position;
        }
    }

    public static GameObject? GetNeighbor(VCEquipmentSlotBase slot, string direction)
    {
        var navigation = Navigation.GetValue(slot);
        return (GameObject?)AccessTools.Field(navigation.GetType(), direction).GetValue(navigation);
    }
    public static void SetNeighbor(VCEquipmentSlotBase slot, string direction, GameObject? target)
    {
        var navigation = Navigation.GetValue(slot);
        AccessTools.Field(navigation.GetType(), direction).SetValue(navigation, target);
        Navigation.SetValue(slot, navigation);
    }
}
