using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class SaltVfxTests
{
    GameObject owner;
    readonly Vector3 origin = new Vector3(1500,1500,1500);
    [SetUp] public void SetUp() { owner = new GameObject("Salt VFX QA owner"); owner.transform.position = origin; }
    [TearDown] public void TearDown()
    {
        foreach(var burst in Object.FindObjectsByType<SaltVfxBurst>(FindObjectsSortMode.None))
            Object.DestroyImmediate(burst.gameObject);
        Object.DestroyImmediate(owner);
    }
    [Test] public void WorldSprayHasTwoLayersAndSurvivesOwnerRemoval()
    {
        Assert.IsNotNull(SaltVfxSettings.Default);
        Assert.IsTrue(SaltVfxBurst.Spray(owner,origin,new Ray(origin,Vector3.forward),3,.2f));
        var burst = Object.FindObjectsByType<SaltVfxBurst>(FindObjectsSortMode.None).Single();
        var systems = burst.GetComponentsInChildren<ParticleSystem>();
        Assert.AreEqual(2, systems.Length);
        Assert.AreEqual(SaltVfxSettings.Default.SprayGrains+SaltVfxSettings.Default.SprayPowder, systems.Sum(s=>s.particleCount));
        Assert.IsNull(burst.transform.parent);
        Assert.IsTrue(systems.All(s=>s.main.simulationSpace==ParticleSystemSimulationSpace.World));
        Assert.IsTrue(systems.All(s=>s.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported));
        Object.DestroyImmediate(owner); owner=null;
        Assert.IsNotNull(burst);
    }
    [Test] public void ThirdPersonCameraBehindHandDoesNotCollapseSprayLifetime()
    {
        Assert.IsTrue(SaltVfxBurst.Spray(owner,origin,new Ray(origin-Vector3.forward*5,Vector3.forward),3,.2f));
        var burst=Object.FindFirstObjectByType<SaltVfxBurst>();
        var grains=burst.GetComponentsInChildren<ParticleSystem>()[0];
        var particles=new ParticleSystem.Particle[180]; int count=grains.GetParticles(particles);
        Assert.Greater(count,0);
        for(int i=0;i<count;i++)
        {
            Assert.Greater(particles[i].startLifetime,.3f,"Camera setback must not reduce a 3m hand spray to 4cm");
            Assert.Greater(Vector3.Dot(particles[i].velocity,Vector3.forward),0);
        }
    }
    [Test] public void ConfirmedHitDeduplicatesHostEchoAndRejectsInvalidCoordinates()
    {
        var presentation=owner.AddComponent<PlayerSaltHitPresentation>();
        presentation.PresentConfirmedHit(1,0,origin,Vector3.back);
        presentation.PresentConfirmedHit(1,0,origin,Vector3.back);
        presentation.PresentConfirmedHit(1,1,new Vector3(float.NaN,0,0),Vector3.back);
        Assert.AreEqual(1,Object.FindObjectsByType<SaltVfxBurst>(FindObjectsSortMode.None).Count(b=>b.IsImpact));
        presentation.PresentConfirmedHit(1,1,origin,Vector3.back);
        Assert.AreEqual(2,Object.FindObjectsByType<SaltVfxBurst>(FindObjectsSortMode.None).Count(b=>b.IsImpact));
    }
    [Test] public void WallLimitsParticleTravelAndBurstBudgetIsBounded()
    {
        var wall = new GameObject("Salt QA wall");
        try
        {
            wall.transform.position=origin+Vector3.forward*.6f;
            var box=wall.AddComponent<BoxCollider>(); box.size=new Vector3(4,4,.1f);
            Physics.SyncTransforms();
            Assert.IsTrue(SaltVfxBurst.Spray(owner,origin,new Ray(origin,Vector3.forward),3,.2f));
            var ps=Object.FindFirstObjectByType<SaltVfxBurst>().GetComponentsInChildren<ParticleSystem>()[0];
            var particles=new ParticleSystem.Particle[180]; int count=ps.GetParticles(particles);
            Assert.Greater(count,0);
            for(int i=0;i<count;i++) Assert.LessOrEqual(particles[i].velocity.magnitude*particles[i].startLifetime,.55f);
            for(int i=0;i<35;i++) SaltVfxBurst.Impact(origin,Vector3.back);
            Assert.LessOrEqual(Object.FindObjectsByType<SaltVfxBurst>(FindObjectsSortMode.None).Length,SaltVfxSettings.Default.MaxLiveBursts);
        }
        finally { Object.DestroyImmediate(wall); }
    }
}
