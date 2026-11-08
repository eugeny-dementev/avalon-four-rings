using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Awaken.TG.Main.Character;
using Awaken.TG.Main.Heroes;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Equipment;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Loadouts;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Panel;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Panel.List;
using Awaken.TG.Main.Heroes.CharacterSheet.Items.Panel.Slot;
using Awaken.TG.Main.Heroes.Items;
using Awaken.TG.Main.Heroes.Items.LootTables;
using Awaken.TG.Main.Heroes.Stats;
using Awaken.TG.Main.Saving.SaveSlots;
using Awaken.TG.Main.Saving;
using Awaken.TG.Main.Skills;
using Awaken.TG.Main.Skills.Passives;
using Awaken.TG.Main.Skills.Units.Passives;
using Awaken.TG.MVC.Events;
using Awaken.Utility.Collections;
using Awaken.TG.Main.UI;
using Awaken.TG.Main.Utility;
using Awaken.TG.Main.Utility.UI;
using HarmonyLib;
using Awaken.TG.MVC;
using Awaken.TG.MVC.UI;
using Awaken.TG.MVC.UI.Events;
using Awaken.TG.MVC.UI.Handlers.Focuses;
using BepInEx;
using UnityEngine;

namespace FourRings;

// Compiled only with IncludeHarness=true; never included in the delivered plugin.
[BepInPlugin("local.avalon.fourrings.tests", "Four Rings Test Harness", "1.0.0")]
[BepInDependency("local.avalon.fourrings")]
public sealed class TestHarness : BaseUnityPlugin
{
    static readonly string Root = Environment.GetEnvironmentVariable("FOUR_RINGS_TEST_DIRECTORY") ?? "";
    const string Confirmation = "I CONFIRM THE ACTIVE GAME PROFILE IS DISPOSABLE";
    float nextPoll;
    bool busy;
    bool? originalHorseDlc;
    static HeroItems Inventory => Hero.Current.HeroItems;
    static Item[] Rings => Inventory.Items.Where(i => i.EquipmentType == EquipmentType.Ring).ToArray();
    void Awake()
    {
        enabled = TestSessionConfirmed();
        Logger.LogWarning(enabled ? "Disposable-save test harness active; remove this build after testing." : "Test harness disabled: disposable-profile confirmation is missing.");
    }
    static bool TestSessionConfirmed()
    {
        if (!Path.IsPathRooted(Root)) return false;
        var guard = Path.Combine(Root, "test-session.active");
        return File.Exists(guard) && File.ReadAllText(guard).Trim() == Confirmation;
    }
    void Update()
    {
        if (busy || Time.realtimeSinceStartup < nextPoll || !TestSessionConfirmed()) return;
        nextPoll = Time.realtimeSinceStartup + 0.5f;
        var commandFile = Path.Combine(Root, "command.txt");
        if (!File.Exists(commandFile)) return;
        var command = File.ReadAllText(commandFile).Trim();
        File.Delete(commandFile);
        try
        {
            if (command == "quit") { Application.Quit(); return; }
            if (command == "load") { LoadSave.Get.Load(SaveSlot.LastSaveSlot, "Four Rings test"); Log("LOAD REQUEST " + SaveSlot.LastSaveSlot.SaveFileName); return; }
            Check(Hero.Current != null, "hero loaded");
            switch (command)
            {
                case "dump": Dump(); break;
                case "layout": LayoutTests(); break;
                case "hide-horse":
                    var handler = Hero.Current!.Element<HeroHorseArmorHandler>();
                    var dlc = AccessTools.Field(typeof(HeroHorseArmorHandler), "_usingHorseArmorDlc");
                    originalHorseDlc ??= (bool)dlc.GetValue(handler);
                    dlc.SetValue(handler, false);
                    Log("Reopen inventory to inspect the hidden horse control."); break;
                case "restore-horse":
                    Check(originalHorseDlc.HasValue, "horse visibility override exists");
                    AccessTools.Field(typeof(HeroHorseArmorHandler), "_usingHorseArmorDlc").SetValue(Hero.Current!.Element<HeroHorseArmorHandler>(), originalHorseDlc!.Value);
                    originalHorseDlc = null;
                    Log("Reopen inventory to restore native horse visibility."); break;
                case "test": busy = true; StartCoroutine(RunProtected(EquipmentTests())); break;
                case "controller": busy = true; StartCoroutine(RunProtected(ControllerTests())); break;
                case "inventory": busy = true; StartCoroutine(RunProtected(InventoryTests())); break;
                case "effects": busy = true; StartCoroutine(RunProtected(EffectTests())); break;
                case "clone":
                    Check(Rings.Length == 3, "exactly three original rings");
                    var added = ItemUtils.AddSingleItem(Inventory, new ItemSpawningDataRuntime(Rings[0]) { quantity = 1, favourite = false });
                    Check(Rings.Length == 4 && Rings.Select(i => i.ID).Distinct().Count() == 4, "four separate item instances");
                    Log("Temporary fourth ring: " + added.ID); break;
                case "four":
                    Clear();
                    var all = Rings;
                    Check(all.Length >= 4, "at least four rings available");
                    for (int i = 0; i < 4; i++) Equip(all[i], EquipmentSlotType.Rings[i]);
                    Dump(); break;
                case "clean": Clear(); Dump(); break;
                case "save":
                    var save = SaveSlot.CreateAndSave("Four Rings Test", false);
                    Log("SAVE REQUEST " + save.SaveFileName); break;
                case "navigate": NavigationTests(); break;
                case "verify":
                    Check(EquipmentSlotType.Rings.All(s => Inventory.EquippedItem(s) != null), "all four assignments survived restart");
                    Check(Rings.Length >= 4 && Rings.Select(i => i.ID).Distinct().Count() == Rings.Length, "inventory rings retain distinct identities after restart");
                    Dump(); break;
                default: throw new InvalidOperationException("Unknown test command: " + command);
            }
            Log("DONE " + command);
        }
        catch (Exception e) { Log("FAIL " + command + " " + e); }
    }
    IEnumerator RunProtected(IEnumerator test)
    {
        while (true)
        {
            object? next;
            try { if (!test.MoveNext()) break; next = test.Current; }
            catch (Exception e) { Log("FAIL equipment tests " + e); break; }
            yield return next;
        }
        busy = false;
        Log("DONE equipment tests");
    }
    IEnumerator EquipmentTests()
    {
        var rings = Rings;
        Check(rings.Length >= 3, "at least three rings available");
        var originalIds = rings.Select(i => i.ID).OrderBy(i => i).ToArray();
        Clear(); yield return null; yield return null;
        var baseline = Stats();
        foreach (var ring in rings.Take(3))
        {
            Dictionary<string, float>? expected = null;
            foreach (var slot in EquipmentSlotType.Rings)
            {
                Equip(ring, slot); yield return null; yield return null;
                var actual = Stats();
                if (expected == null) expected = actual;
                else EqualStats(expected, actual, "same ring effects in " + slot.EnumName);
                Check(Inventory.EquippedItem(slot) == ring && ring.IsEquipped, "ring assigned " + ring.ID + " to " + slot.EnumName);
                Log("ELIGIBILITY " + slot.EnumName + " sell=" + Inventory.SellableInventory(null).Contains(ring) + " stash=" + Inventory.StashableInventory.Contains(ring) + " drop=" + ring.IsDroppable);
                Inventory.Unequip(slot, null); yield return null; yield return null;
                EqualStats(baseline, Stats(), "baseline restored after " + slot.EnumName);
            }
        }
        for (int empty = 0; empty < 4; empty++)
        {
            Clear();
            var active = EquipmentSlotType.Rings.Where((s, index) => index != empty).ToArray();
            for (int i = 0; i < 3; i++) Equip(rings[i], active[i]);
            yield return null; yield return null;
            Check(active.All(s => Inventory.EquippedItem(s) != null), "three rings with empty slot " + (empty + 1));
        }
        Clear(); yield return null; yield return null;
        EqualStats(baseline, Stats(), "baseline restored after all combinations");
        Check(originalIds.SequenceEqual(Rings.Select(i => i.ID).OrderBy(i => i)), "no lost or duplicated items");
        var nonRing = Inventory.Items.First(i => i.EquipmentType != EquipmentType.Ring);
        Check(!EquipmentSlotType.Rings[2].Accept(nonRing, null) && !EquipmentSlotType.Rings[3].Accept(nonRing, null), "new slots reject non-rings");
        for (int i = 0; i < 3; i++) Equip(rings[i], EquipmentSlotType.Rings[i]);
        Log("PASS equipment matrix completed");
    }
    static void Equip(Item item, EquipmentSlotType slot)
    {
        Check(Inventory.Equip(item, slot, null), "native Equip returned true for " + slot.EnumName);
    }
    static IEnumerator EffectTests()
    {
        Clear(); yield return null; yield return null;
        foreach (var ring in Rings)
        {
            foreach (var skill in ring.ActiveSkills)
                Log("SKILL " + ring.ID + " " + skill.Graph.name + " units=" + string.Join(",", skill.Machine.graph.units.OfType<IPassiveUnit>().Select(u => u.GetType().Name + ":" + u.Type)));
            var baseline = Effects(ring);
            var sellable = Inventory.SellableInventory(null).Contains(ring);
            string? expected = null;
            foreach (var slot in EquipmentSlotType.Rings)
            {
                Equip(ring, slot); yield return null; yield return null;
                var actual = Effects(ring);
                if (expected == null) expected = actual;
                else Check(actual == expected, "effect and listener parity for " + ring.ID + " in " + slot.EnumName);
                Check(!Inventory.SellableInventory(null).Contains(ring), "equipped ring excluded from selling in " + slot.EnumName);
                Inventory.Unequip(slot, null); yield return null; yield return null;
                Check(Effects(ring) == baseline, "effect registrations return to baseline after " + slot.EnumName);
                Check(Inventory.SellableInventory(null).Contains(ring) == sellable, "unequip restores selling eligibility");
            }
        }
        Log("PASS effect lifecycle matrix completed");
    }
    static string Effects(Item ring)
    {
        var owners = (Dictionary<IListenerOwner, StructList<IEventListener>>)AccessTools.Field(typeof(Awaken.TG.MVC.Events.EventSystem), "_byOwner").GetValue(World.EventSystem);
        var result = new List<string>();
        foreach (var skill in ring.ActiveSkills)
        {
            result.Add(skill.ID + ":" + skill.IsLearned + ":" + skill.IsEquipped + ":" + skill.IsSubmitted);
            foreach (var effect in skill.Elements<IPassiveEffect>()) result.Add(skill.ID + ":effect:" + effect.GetType().Name);
            if (owners.TryGetValue(skill, out var listeners))
                for (int i = 0; i < listeners.Count; i++)
                    if (!listeners[i].ShouldBeDisposed) result.Add(skill.ID + ":listener:" + listeners[i].Selector);
        }
        return string.Join("|", result.OrderBy(s => s, StringComparer.Ordinal));
    }
    static void Clear() { foreach (var slot in EquipmentSlotType.Rings) Inventory.Unequip(slot, null); }
    static Dictionary<string, float> Stats()
    {
        var result = new Dictionary<string, float>();
        foreach (var group in new object[] { Hero.Current.HeroStats, Hero.Current.HeroRPGStats, Hero.Current.CharacterStats, Hero.Current.AliveStats, Hero.Current.StatusStats, Hero.Current.HeroMultStats })
        foreach (var property in group.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!typeof(Stat).IsAssignableFrom(property.PropertyType) || property.GetIndexParameters().Length != 0) continue;
            if (property.GetValue(group) is Stat stat && !(stat is LimitedStat)) result[group.GetType().Name + "." + property.Name] = stat.ModifiedValue;
        }
        return result;
    }
    static void EqualStats(Dictionary<string, float> expected, Dictionary<string, float> actual, string label)
    {
        var differences = expected.Where(p => !actual.ContainsKey(p.Key) || Math.Abs(actual[p.Key] - p.Value) > 0.001f).Select(p => p.Key + ": " + p.Value + " -> " + actual[p.Key]).ToArray();
        Check(differences.Length == 0, label + (differences.Length == 0 ? "" : ": " + string.Join(", ", differences)));
    }
    static void Dump()
    {
        foreach (var ring in Rings) Log("RING " + ring.ID + " " + ring.DisplayName + " equipped=" + ring.IsEquipped + " slot=" + ring.EquippedInSlotOfType?.EnumName);
        foreach (var slot in EquipmentSlotType.Rings) Log("SLOT " + slot.EnumName + "=" + Inventory.EquippedItem(slot)?.ID);
        foreach (var stat in Stats()) Log("STAT " + stat.Key + "=" + stat.Value.ToString("R", CultureInfo.InvariantCulture));
        foreach (var slot in UnityEngine.Object.FindObjectsByType<VCEquipmentSlotBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var r = (RectTransform)slot.transform;
            var corners = new Vector3[4]; r.GetWorldCorners(corners);
            Log("UI " + slot.Type.EnumName + " parent=" + slot.transform.parent.name + " xy=" + r.anchoredPosition + " size=" + r.rect.size + " world=" + string.Join(";", corners.Select(c => c.ToString())));
        }
    }
    static void LayoutTests()
    {
        var slots = UnityEngine.Object.FindObjectsByType<VCEquipmentSlotBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var relevant = EquipmentSlotType.Rings.Concat(new[] { EquipmentSlotType.Boots, EquipmentSlotType.HorseArmor }).ToArray();
        var bounds = relevant.ToDictionary(type => type, type =>
        {
            var control = slots.Single(s => s.Type == type);
            var corners = new Vector3[4];
            ((RectTransform)control.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(c => c.x), corners.Min(c => c.y), corners.Max(c => c.x), corners.Max(c => c.y));
        });
        foreach (var first in relevant)
        {
            var rect = bounds[first];
            Check(rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= Screen.width && rect.yMax <= Screen.height, "control remains on screen: " + first.EnumName);
            foreach (var second in relevant.Where(s => s != first))
                Check(!rect.Overlaps(bounds[second]), "no overlap: " + first.EnumName + " / " + second.EnumName);
        }
        var horse = bounds[EquipmentSlotType.HorseArmor];
        var ring3 = bounds[EquipmentSlotType.Rings[2]];
        Check(horse.xMax < ring3.xMin && Math.Abs(horse.center.y - ring3.center.y) < 1, "horse control sits left of lower-left ring");
        Check(slots.Where(s => relevant.Contains(s.Type)).Sum(s => s.GetComponents<Plugin.SlotAlignment>().Length) == 3, "exactly three alignment components in the active equipment view");
        Log("PASS equipment layout");
    }
    static void NavigationTests()
    {
        var slots = UnityEngine.Object.FindObjectsByType<VCEquipmentSlotBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var focus = World.Only<Focus>();
        var gameUI = World.Only<GameUI>();
        var controllerType = AccessTools.Field(typeof(RewiredHelper), "s_controllerType");
        var checkedFrame = AccessTools.Field(typeof(RewiredHelper), "s_lastFrameGamepadCheck");
        var previousType = controllerType.GetValue(null);
        var previousFrame = checkedFrame.GetValue(null);
        try
        {
        controllerType.SetValue(null, Rewired.ControllerType.Joystick);
        checkedFrame.SetValue(null, Time.frameCount);
        foreach (var native in new[] { EquipmentSlotType.Ring1, EquipmentSlotType.Ring2 })
        {
            var source = slots.Single(s => s.Type == native);
            var destination = Plugin.GetNeighbor(source, "down")!.GetComponent<VCDefaultEquipmentSlot>();
            Check(focus.BelongsToFocusBase(source) && focus.BelongsToFocusBase(destination), "ring controls belong to active focus base");
            focus.Select(source, false);
            source.Handle(new UINaviAction { direction = NaviDirection.Down, GameUI = gameUI });
            Check(focus.Focused == destination, "controller navigation to " + destination.Type.EnumName);
            destination.Handle(new UINaviAction { direction = NaviDirection.Up, GameUI = gameUI });
            Check(focus.Focused == source, "controller navigation back to " + source.Type.EnumName);
        }
        var boots = slots.Single(s => s.Type == EquipmentSlotType.Boots);
        foreach (var direction in new[] { "left", "right" })
        {
            var destination = Plugin.GetNeighbor(boots, direction)!.GetComponent<VCDefaultEquipmentSlot>();
            focus.Select(boots, false);
            boots.Handle(new UINaviAction { direction = direction == "left" ? NaviDirection.Left : NaviDirection.Right, GameUI = gameUI });
            Check(focus.Focused == destination, "controller navigation from boots to " + destination.Type.EnumName);
            destination.Handle(new UINaviAction { direction = direction == "left" ? NaviDirection.Right : NaviDirection.Left, GameUI = gameUI });
            Check(focus.Focused == boots, "controller navigation back to boots");
        }
        var horse = slots.Single(s => s.Type == EquipmentSlotType.HorseArmor);
        var ring3 = slots.Single(s => s.Type == EquipmentSlotType.Rings[2]);
        Check(Plugin.GetNeighbor(ring3, "left") == horse.gameObject && Plugin.GetNeighbor(horse, "right") == ring3.gameObject, "bidirectional horse and ring navigation links");
        focus.Select(ring3, false);
        ring3.Handle(new UINaviAction { direction = NaviDirection.Left, GameUI = gameUI });
        if (Hero.Current.Element<HeroHorseArmorHandler>().SlotHidden)
            Check(focus.Focused != horse, "navigation skips hidden horse control");
        else
        {
            Check(focus.Focused == horse, "controller reaches horse control");
            horse.Handle(new UINaviAction { direction = NaviDirection.Right, GameUI = gameUI });
            Check(focus.Focused == ring3, "controller returns from horse to third ring");
        }
        }
        finally
        {
            controllerType.SetValue(null, previousType);
            checkedFrame.SetValue(null, previousFrame);
        }
    }
    static UIKeyDownAction KeyAction(string name) => new UIKeyDownAction { Data = new InputActionData { actionName = name, actionId = Rewired.ReInput.mapping.GetActionId(name), player = RewiredHelper.Player } };
    static IEnumerator ControllerTests()
    {
        NavigationTests();
        var controls = UnityEngine.Object.FindObjectsByType<VCEquipmentSlotBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var slot in EquipmentSlotType.Rings.Concat(new[] { EquipmentSlotType.HorseArmor }))
        {
            Clear(); yield return null;
            var control = controls.Single(c => c.Type == slot);
            var loadouts = (LoadoutsUI)control.Target;
            Check(!control.Locked, "test equipment control is unlocked: " + slot.EnumName);
            Inventory.Unequip(slot, null);
            var ring = Inventory.Items.First(i => i.EquipmentType == (slot == EquipmentSlotType.HorseArmor ? EquipmentType.HorseArmor : EquipmentType.Ring));
            control.Handle(new UIEPointTo());
            loadouts.CurrentlyHoveredSlot.OnStartHover(control);
            Check(loadouts.Prompts != null && ReferenceEquals(loadouts.CurrentlyHoveredSlot.Get, control), "native prompts target " + slot.EnumName);
            loadouts.Prompts!.Handle(KeyAction(KeyBindings.UI.Items.SelectItem));
            yield return new WaitForSecondsRealtime(0.25f);
            var chooser = World.Any<EquipmentChooseUI>();
            Check(chooser != null && chooser.EquipmentSlotType == slot, "controller opens equipment chooser for " + slot.EnumName);
            var list = chooser!.Element<ItemsUI>();
            var entry = World.All<ItemsListElementUI>().First(e => e.Item == ring && e.ItemsUI == list);
            entry.OnHoverStarted();
            entry.View<VItemsListElement>().Handle(new UISubmitAction());
            yield return null; yield return null;
            Check(Inventory.EquippedItem(slot) == ring, "controller submit equips " + slot.EnumName);
            string cancel = KeyBindings.UI.Generic.Cancel;
            World.Services.Get<GlobalKeys>().Handle(new UICancelAction { Data = new InputActionData { actionName = cancel, actionId = Rewired.ReInput.mapping.GetActionId(cancel), player = RewiredHelper.Player } });
            yield return new WaitForSecondsRealtime(0.25f);
            Check(World.Any<EquipmentChooseUI>() == null, "controller cancel closes chooser");
            LayoutTests();
            control.Handle(new UIEPointTo());
            loadouts.CurrentlyHoveredSlot.OnStartHover(control);
            Check(!loadouts.HasBeenDiscarded && loadouts.Prompts != null && ReferenceEquals(loadouts.CurrentlyHoveredSlot.Get, control), "native unequip prompt targets " + slot.EnumName);
            loadouts.Prompts!.Handle(KeyAction(KeyBindings.UI.Items.UnequipItem));
            yield return null;
            Check(Inventory.EquippedItem(slot) == null && !ring.IsEquipped, "controller unequip clears " + slot.EnumName);
        }
        Log("PASS synthetic controller actions completed; physical device untested");
    }
    static IEnumerator InventoryTests()
    {
        var rings = Rings;
        Check(rings.Length == 4, "four-ring disposable save loaded");
        var ring = rings[3];
        var slot = EquipmentSlotType.Rings[3];
        Inventory.Unequip(ring, null);
        yield return null;
        var baseline = Stats();
        Equip(ring, slot); yield return null;
        Check(!Inventory.SellableInventory(null).Contains(ring), "equipped extra ring excluded from selling");
        Check(Inventory.StashableInventory.Contains(ring), "extra ring follows native stash eligibility");
        ItemUtils.MoveTo(ring, Hero.Current.Storage);
        yield return null; yield return null;
        Check(Inventory.EquippedItem(slot) == null && !ring.IsEquipped && Hero.Current.Storage.Items.Contains(ring), "storage transfer clears equipment");
        EqualStats(baseline, Stats(), "storage transfer removes effects");
        ring = ItemUtils.MoveTo(ring, Inventory);
        Check(Rings.Length == 4, "storage return preserves item count");
        Equip(ring, slot); yield return null;
        var control = UnityEngine.Object.FindObjectsByType<VCDefaultEquipmentSlot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Single(c => c.Type == slot);
        control.Handle(new UIEPointTo());
        World.Any<LoadoutsUI>().Prompts.Handle(KeyAction(KeyBindings.UI.Items.DropItem));
        yield return null; yield return null;
        Check(Inventory.EquippedItem(slot) == null && Rings.Length == 3, "native drop prompt clears slot and removes test ring");
        EqualStats(baseline, Stats(), "dropping removes effects");
        Log("PASS storage and drop tests; reload four-ring test save before further tests");
    }
    static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); Log("PASS " + label); }
    static void Log(string text) { File.AppendAllText(Path.Combine(Root, "runtime-tests.log"), DateTime.UtcNow.ToString("O") + " " + text + Environment.NewLine); }
}
