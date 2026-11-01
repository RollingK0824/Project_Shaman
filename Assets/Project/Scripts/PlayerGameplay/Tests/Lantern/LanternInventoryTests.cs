#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class LanternInventoryTests
{
    const string D="Assets/Project/Data/Items/";
    readonly List<GameObject> _objects=new();
    PlayerInventory inv; PlayerCraftingController craft; SharedMaterialInventory shared;
    ItemData lamp,oil,paper,salt;TalismanItemData blank;CraftingRecipeData recipe;
    T Load<T>(string path) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(D+path) ?? AssetDatabase.LoadAssetAtPath<T>(D+"Items/"+path);
    GameObject Make(string name){var o=new GameObject(name);_objects.Add(o);return o;}
    [SetUp] public void SetUp()
    {
        shared=Make("QA Shared").AddComponent<SharedMaterialInventory>();
        inv=Make("QA Crafter").AddComponent<PlayerInventory>();inv.BindSharedMaterials(shared);craft=inv.gameObject.AddComponent<PlayerCraftingController>();
        lamp=Load<ItemData>("LanternItemData.asset");oil=Load<ItemData>("LampOilMaterial.asset");paper=Load<ItemData>("PaperMaterial.asset");salt=Load<ItemData>("CoarseSaltMaterial.asset");blank=Load<TalismanItemData>("Talismans/RegistryTalisman.asset");recipe=Load<CraftingRecipeData>("LanternRecipe.asset");
        Assert.IsNotNull(recipe);
        // Keep the historical mixed-resource transaction regression fixture,
        // independently of the now-retired production lantern recipe.
        recipe = UnityEngine.Object.Instantiate(recipe);
        var serialized = new SerializedObject(recipe);
        serialized.FindProperty("_disabled").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    [TearDown] public void TearDown(){if(recipe)UnityEngine.Object.DestroyImmediate(recipe);for(int i=_objects.Count-1;i>=0;i--)if(_objects[i])UnityEngine.Object.DestroyImmediate(_objects[i]);_objects.Clear();}
    void Fill(int paperCount=2,int oilCount=1,int saltCount=1,int blankCount=1)
    {if(paperCount>0)Assert.IsTrue(inv.TryAddItem(paper,paperCount));if(oilCount>0)Assert.IsTrue(inv.TryAddItem(oil,oilCount));if(saltCount>0)Assert.IsTrue(inv.TryAddItem(salt,saltCount));if(blankCount>0)Assert.IsTrue(inv.TryAddItem(blank,blankCount));}
    int[] Counts()=>new[]{inv.GetItemCount(paper),inv.GetItemCount(oil),inv.GetItemCount(salt),inv.GetItemCount(blank),inv.GetItemCount(lamp)};
    [Test] public void ProductionLanternRecipeRejectsEvenWithAllMaterials()
    {
        Fill(); var before=Counts(); var production=Load<CraftingRecipeData>("LanternRecipe.asset");
        Assert.IsFalse(production.IsAvailable); Assert.IsFalse(craft.RequestCraft(production));
        Assert.IsFalse(craft.TryCraft(production)); CollectionAssert.AreEqual(before,Counts());
    }
    [Test] public void OilPickupIsSharedOnlyAndCannotEquip()
    {
        Make("QA Offline Judge").AddComponent<LocalGameplayJudge>();
        var world=UnityEngine.Object.Instantiate(oil.WorldPrefab);_objects.Add(world);
        world.GetComponent<WorldItem>().Interact(inv.gameObject);
        Assert.AreEqual(1,shared.GetCount(oil));Assert.IsFalse(inv.Items.Contains(oil));Assert.IsFalse(world.activeSelf);
        Assert.IsFalse(inv.AssignQuickSlot(0,oil));Assert.IsNull(oil.VisualPrefab);Assert.IsNull(oil.UseAction);
        for(int i=0;i<6;i++)Assert.AreNotEqual(oil,inv.GetQuickSlotItem(i));
    }
    [Test] public void MixedCraftCommitsAllCountsBeforeObserversAndAutoSlots()
    {
        Fill();bool observed=false;int[] observedCounts=null;long version=shared.Version;
        inv.ItemAdded+=item=>{if(item!=lamp)return;observed=true;observedCounts=Counts();};
        Assert.IsTrue(craft.TryCraft(recipe));Assert.IsTrue(observed);CollectionAssert.AreEqual(new[]{0,0,0,0,1},observedCounts);Assert.AreEqual(version+1,shared.Version);
        Assert.IsTrue(Enumerable.Range(0,6).Any(i=>inv.GetQuickSlotItem(i)==lamp));
        Assert.IsTrue(lamp.IsPermanent&&lamp.IsUnique);Assert.IsFalse(inv.ConsumeItem(lamp));Assert.IsFalse(inv.TryAddItem(lamp));
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void EachMissingCostPreservesEverything(int missing)
    {
        Fill(missing==0?1:2,missing==1?0:1,missing==2?0:1,missing==3?0:1);
        var before=Counts();long version=shared.Version;Assert.IsFalse(craft.TryCraft(recipe));CollectionAssert.AreEqual(before,Counts());Assert.AreEqual(version,shared.Version);
    }
    [Test] public void DuplicateLanternPreservesCosts()
    {Fill();Assert.IsTrue(inv.TryAddItem(lamp));var before=Counts();long version=shared.Version;Assert.IsTrue(craft.AlreadyOwnsUniqueResult(recipe));Assert.IsFalse(craft.TryCraft(recipe));CollectionAssert.AreEqual(before,Counts());Assert.AreEqual(version,shared.Version);}
    [Test] public void FullQuickSlotsStillDeliverResultToPersonalInventory()
    {
        string[] names={"BellItemData.asset","SaltItemData.asset","CoinItemData.asset","MirrorItemData.asset","GongItemData.asset","BeanMugwortItemData.asset"};
        foreach(string name in names)Assert.IsTrue(inv.TryAddItem(Load<ItemData>(name)));
        var before=Enumerable.Range(0,6).Select(inv.GetQuickSlotItem).ToArray();Fill();Assert.IsTrue(craft.TryCraft(recipe));Assert.AreEqual(1,inv.GetItemCount(lamp));CollectionAssert.AreEqual(before,Enumerable.Range(0,6).Select(inv.GetQuickSlotItem));
    }
    [Test] public void OnlyOtherPlayersBlankCannotPay()
    {
        Fill(blankCount:0);var other=Make("Other player").AddComponent<PlayerInventory>();other.BindSharedMaterials(shared);other.TryAddItem(blank);
        var before=Counts();Assert.IsFalse(craft.TryCraft(recipe));CollectionAssert.AreEqual(before,Counts());Assert.AreEqual(1,other.GetItemCount(blank));
    }
    [Test] public void DrawnTalismanAndBarrierAreNotIngredients()
    {
        var collection=inv.gameObject.AddComponent<PlayerTalismanCollection>();Fill();
        var draft=collection.CreateDraft(blank);draft.BeginStroke(new Vector2(.3f,.3f));draft.AppendPoint(new Vector2(.7f,.7f));draft.EndStroke();
        Assert.IsTrue(collection.Save(blank,draft,"보존 부적",blank.Effects[0].EffectId));
        Assert.IsTrue(inv.TryAddItem(Load<ItemData>("Talismans/BarrierTalisman.asset")));
        var before=Counts();Assert.IsFalse(craft.TryCraft(recipe));CollectionAssert.AreEqual(before,Counts());Assert.AreEqual(1,collection.DrawnEffectCount);
        Assert.IsTrue(inv.TryAddItem(blank));Assert.IsTrue(craft.TryCraft(recipe));Assert.AreEqual(1,collection.DrawnEffectCount);
        Assert.IsTrue(collection.RequestUse(draft.Id));Assert.AreEqual(0,collection.DrawnEffectCount);
    }
    [Test] public void RequestAndConfirmationStaySeparate()
    {
        Fill();int requests=0;craft.SetAuthorityCheck(()=>false);craft.CraftRequested+=r=>{Assert.AreSame(recipe,r);requests++;};
        var before=Counts();long version=shared.Version;Assert.IsTrue(craft.RequestCraft(recipe));Assert.AreEqual(1,requests);CollectionAssert.AreEqual(before,Counts());Assert.AreEqual(version,shared.Version);
        craft.SetAuthorityCheck(()=>true);Assert.IsTrue(craft.TryCraft(recipe));CollectionAssert.AreEqual(new[]{0,0,0,0,1},Counts());
    }
    [TestCase(true)] [TestCase(false)]
    public void NetworkCraftResultClearsPendingEvenWhenPanelIsClosed(bool success)
    {
        Fill();
        craft.SetAuthorityCheck(() => false);
        craft.CraftRequested += _ => { };
        var panelObject = Make("Craft panel");
        var panel = panelObject.AddComponent<CraftingPanelUI>();
        panel.Bind(craft);
        panel.SetRecipes(new[] { recipe });
        panel.CraftSelected();
        Assert.IsTrue(panel.HasPendingRequest);
        panelObject.SetActive(false);
        craft.NotifyCraftResult(recipe, success);
        Assert.IsFalse(panel.HasPendingRequest);
        CollectionAssert.AreEqual(new[] { 2, 1, 1, 1, 0 }, Counts());
    }
    [Test] public void DeniedSharedAuthorityPreservesPersonalCost()
    {Fill();shared.SetAuthorityCheck(()=>false);var before=Counts();Assert.IsFalse(craft.TryCraft(recipe));CollectionAssert.AreEqual(before,Counts());}
    [Test] public void ReentrantAndCompetingCraftCannotDoubleSpend()
    {
        Fill();var other=Make("Competing player").AddComponent<PlayerInventory>();other.BindSharedMaterials(shared);other.TryAddItem(blank);var otherCraft=other.gameObject.AddComponent<PlayerCraftingController>();bool reentered=false, reentrantResult=true;
        inv.ItemAdded+=item=>{if(item!=lamp)return;reentered=true;reentrantResult=otherCraft.TryCraft(recipe);};
        Assert.IsTrue(craft.TryCraft(recipe));Assert.IsTrue(reentered);Assert.IsFalse(reentrantResult);Assert.IsFalse(otherCraft.TryCraft(recipe));Assert.AreEqual(1,other.GetItemCount(blank));Assert.AreEqual(0,other.GetItemCount(lamp));Assert.AreEqual(1,inv.GetItemCount(lamp));
    }
    [Test] public void UnboundHiddenOrInactiveLanternCannotIlluminate()
    {
        var visual=UnityEngine.Object.Instantiate(lamp.VisualPrefab);_objects.Add(visual);
        var item=visual.GetComponent<LanternItem>();var light=visual.GetComponentInChildren<Light>();var surface=visual.GetComponentInChildren<Renderer>();
        var update=typeof(LanternItem).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        update.Invoke(item,null);Assert.IsFalse(light.enabled);
        item.BindItemVisual(lamp,inv.gameObject);update.Invoke(item,null);Assert.IsTrue(light.enabled);
        surface.enabled=false;update.Invoke(item,null);Assert.IsFalse(light.enabled);
        surface.enabled=true;inv.gameObject.SetActive(false);update.Invoke(item,null);Assert.IsFalse(light.enabled);
        inv.gameObject.SetActive(true);item.OnUnequipped();Assert.IsFalse(light.enabled);
    }
    [Test] public void ExistingFourRecipesStillCraft()
    {
        foreach(var path in new[]{"SaltRecipe.asset","BeanMugwortRecipe.asset","Resources/PlayerCrafting/RegistryTalismanRecipe.asset","Resources/PlayerCrafting/BarrierTalismanRecipe.asset"})
        {
            var old=Load<CraftingRecipeData>(path);var costs=new Dictionary<ItemData,int>();Assert.IsTrue(old.TryGetCosts(costs));foreach(var c in costs)Assert.IsTrue(inv.TryAddItem(c.Key,c.Value));int before=inv.GetItemCount(old.ResultItem);Assert.IsTrue(craft.TryCraft(old));Assert.AreEqual(before+old.ResultAmount,inv.GetItemCount(old.ResultItem));
        }
    }
}

#endif
