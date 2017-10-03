using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.VFX;
using UnityEditor;
using UnityEngine.TestTools;
using System.Linq;
using UnityEditor.VFX.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.VFX.Block;

namespace UnityEditor.VFX.Test
{
    public class VFXSpawnerTest
    {
        [UnityTest]
        [Timeout(1000 * 10)]
        public IEnumerator CreateAssetAndComponentSpawner()
        {
            EditorApplication.ExecuteMenuItem("Window/Game");
            var graph = ScriptableObject.CreateInstance<VFXGraph>();

            var spawnerContext = ScriptableObject.CreateInstance<VFXBasicSpawner>();
            var blockConstantRate = ScriptableObject.CreateInstance<VFXSpawnerConstantRate>();
            var slotCount = blockConstantRate.GetInputSlot(0);

            var spawnerInit = ScriptableObject.CreateInstance<VFXBasicInitialize>();

            var spawnCountValue = 753.0f;
            slotCount.value = spawnCountValue;

            spawnerContext.AddChild(blockConstantRate);
            graph.AddChild(spawnerContext);
            graph.AddChild(spawnerInit);
            spawnerInit.LinkFrom(spawnerContext);

            graph.vfxAsset = new VFXAsset();
            graph.RecompileIfNeeded();
            graph.vfxAsset.bounds = new Bounds(Vector3.zero, Vector3.positiveInfinity);

            var gameObj = new GameObject("CreateAssetAndComponentSpawner");
            var vfxComponent = gameObj.AddComponent<VFXComponent>();
            vfxComponent.vfxAsset = graph.vfxAsset;

            int maxFrame = 512;
            while (vfxComponent.culled && --maxFrame > 0)
            {
                yield return null;
            }
            Assert.IsTrue(maxFrame > 0);
            yield return null; //wait for exactly one more update if visible

            var spawnerState = vfxComponent.DebugGetSpawnerState(0);

            var spawnCountRead = spawnerState.spawnCount / spawnerState.deltaTime;
            Assert.LessOrEqual(Mathf.Abs(spawnCountRead - spawnCountValue), 0.01f);
            UnityEngine.Object.DestroyImmediate(gameObj);
        }

        [UnityTest]
        [Timeout(1000 * 10)]
        public IEnumerator CreateEventStartAndStop()
        {
            EditorApplication.ExecuteMenuItem("Window/Game");
            var graph = ScriptableObject.CreateInstance<VFXGraph>();

            var eventStart = ScriptableObject.CreateInstance<VFXBasicEvent>();
            eventStart.eventName = "Custom_Start";
            var eventStop = ScriptableObject.CreateInstance<VFXBasicEvent>();
            eventStop.eventName = "Custom_Stop";

            var spawnerContext = ScriptableObject.CreateInstance<VFXBasicSpawner>();
            var blockConstantRate = ScriptableObject.CreateInstance<VFXSpawnerConstantRate>();
            var slotCount = blockConstantRate.GetInputSlot(0);

            var spawnerInit = ScriptableObject.CreateInstance<VFXBasicInitialize>();

            var spawnCountValue = 1984.0f;
            slotCount.value = spawnCountValue;

            spawnerContext.AddChild(blockConstantRate);
            graph.AddChild(eventStart);
            graph.AddChild(eventStop);
            graph.AddChild(spawnerContext);
            graph.AddChild(spawnerInit);
            spawnerInit.LinkFrom(spawnerContext);
            spawnerContext.LinkFrom(eventStart, 0, 0);
            spawnerContext.LinkFrom(eventStop, 0, 1);

            graph.vfxAsset = new VFXAsset();
            graph.RecompileIfNeeded();
            graph.vfxAsset.bounds = new Bounds(Vector3.zero, Vector3.positiveInfinity);

            var gameObj = new GameObject("CreateAssetAndComponentSpawner");
            var vfxComponent = gameObj.AddComponent<VFXComponent>();
            vfxComponent.vfxAsset = graph.vfxAsset;

            int maxFrame = 512;
            while (vfxComponent.culled && --maxFrame > 0)
            {
                yield return null;
            }
            Assert.IsTrue(maxFrame > 0);
            yield return null; //wait for exactly one more update if visible

            var spawnerState = vfxComponent.DebugGetSpawnerState(0);
            var spawnCountRead = spawnerState.spawnCount / spawnerState.deltaTime;
            Assert.LessOrEqual(Mathf.Abs(spawnCountRead), 0.01f);

            vfxComponent.SendEvent("Custom_Start");
            yield return null; //First : Consume
            yield return null; //Second : Update

            spawnerState = vfxComponent.DebugGetSpawnerState(0);
            spawnCountRead = spawnerState.spawnCount / spawnerState.deltaTime;
            Assert.LessOrEqual(Mathf.Abs(spawnCountRead - spawnCountValue), 0.01f);

            vfxComponent.SendEvent("Custom_Stop");
            yield return null; //First : Consume
            yield return null; //Second : Update

            spawnerState = vfxComponent.DebugGetSpawnerState(0);
            spawnCountRead = spawnerState.spawnCount / spawnerState.deltaTime;
            Assert.LessOrEqual(Mathf.Abs(spawnCountRead), 0.01f);

            UnityEngine.Object.DestroyImmediate(gameObj);
        }

        /*
        [UnityTest]
        [Timeout(1000 * 10)]
        public IEnumerator CreateEventAttributeAndStart()
        {
            EditorApplication.ExecuteMenuItem("Window/Game");
            var graph = ScriptableObject.CreateInstance<VFXGraph>();

            var spawnerContext = ScriptableObject.CreateInstance<VFXBasicSpawner>();
            var blockBurst = ScriptableObject.CreateInstance<VFXSpawnerConstantRate>();

            spawnerContext.AddChild(blockBurst);
            graph.AddChild(spawnerContext);

            graph.vfxAsset = new VFXAsset();
            graph.RecompileIfNeeded();
            graph.vfxAsset.bounds = new Bounds(Vector3.zero, Vector3.positiveInfinity);

            var gameObj = new GameObject("CreateEventAttributeAndStart");
            var vfxComponent = gameObj.AddComponent<VFXComponent>();
            vfxComponent.vfxAsset = graph.vfxAsset;

            var lifeTimeIn = 28.0f;
            var vfxEventAttr = vfxComponent.CreateVFXEventAttribute();
            vfxEventAttr.SetFloat("lifeTime", lifeTimeIn);
            vfxComponent.Start(vfxEventAttr);

            int maxFrame = 512;
            while (vfxComponent.culled && --maxFrame > 0)
            {
                yield return null;
            }
            Assert.IsTrue(maxFrame > 0);
            yield return null; //wait for exactly one more update if visible

            var spawnerState = vfxComponent.GetSpawnerState(0);
            var lifeTimeOut = spawnerState.vfxEventAttribute.GetFloat("lifeTime");
            Assert.AreEqual(lifeTimeIn, lifeTimeOut);

            UnityEngine.Object.DestroyImmediate(gameObj);
        }
        */

        [UnityTest]
        [Timeout(1000 * 10)]
        public IEnumerator CreateCustomSpawnerAndComponent()
        {
            EditorApplication.ExecuteMenuItem("Window/Game");
            var graph = ScriptableObject.CreateInstance<VFXGraph>();

            var spawnerContext = ScriptableObject.CreateInstance<VFXBasicSpawner>();
            var blockCustomSpawner = ScriptableObject.CreateInstance<VFXSpawnerCustomWrapper>();
            blockCustomSpawner.Init(typeof(VFXCustomSpawnerTest));

            var spawnerInit = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            var blockSetAttribute = ScriptableObject.CreateInstance<SetAttribute>();
            blockSetAttribute.SetSettingValue("attribute", "lifetime");
            spawnerInit.AddChild(blockSetAttribute);

            var blockAttributeSource = ScriptableObject.CreateInstance<VFXSourceAttributeParameter>();
            blockAttributeSource.SetSettingValue("attribute", "lifetime");

            spawnerContext.AddChild(blockCustomSpawner);
            graph.AddChild(spawnerContext);
            graph.AddChild(spawnerInit);
            graph.AddChild(blockAttributeSource);
            blockAttributeSource.outputSlots[0].Link(blockSetAttribute.inputSlots[0]);

            spawnerInit.LinkFrom(spawnerContext);

            var valueTotalTime = 187.0f;
            blockCustomSpawner.GetInputSlot(0).value = valueTotalTime;

            graph.vfxAsset = new VFXAsset();
            graph.RecompileIfNeeded();
            graph.vfxAsset.bounds = new Bounds(Vector3.zero, Vector3.positiveInfinity);

            var gameObj = new GameObject("CreateAssetAndComponentSpawner");
            var vfxComponent = gameObj.AddComponent<VFXComponent>();
            vfxComponent.vfxAsset = graph.vfxAsset;

            int maxFrame = 512;
            while (vfxComponent.culled && --maxFrame > 0)
            {
                yield return null;
            }
            Assert.IsTrue(maxFrame > 0);
            yield return null; //wait for exactly one more update if visible

            var spawnerState = vfxComponent.DebugGetSpawnerState(0);
            Assert.GreaterOrEqual(spawnerState.totalTime, valueTotalTime);
            Assert.AreEqual(VFXCustomSpawnerTest.s_LifeTime, spawnerState.vfxEventAttribute.GetFloat("lifetime"));
            Assert.AreEqual(VFXCustomSpawnerTest.s_SpawnCount, spawnerState.spawnCount);
        }

        /*
        [UnityTest]
        [Timeout(1000 * 10)]
        public IEnumerator CreateCustomSpawnerLinkedWithSourceAttribute()
        {
            EditorApplication.ExecuteMenuItem("Window/Game");
            var graph = ScriptableObject.CreateInstance<VFXGraph>();

            var spawnerContext = ScriptableObject.CreateInstance<VFXBasicSpawner>();
            graph.AddChild(spawnerContext);

            var initContext = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            graph.AddChild(initContext);

            spawnerContext.LinkTo(initContext);

            var initBlock = ScriptableObject.CreateInstance<VFXAllType>();
            initContext.AddChild(initBlock);

            var attributeVelocity = VFXLibrary.GetSourceAttributeParameters().FirstOrDefault(o => o.name == "velocity").CreateInstance();
            graph.AddChild(attributeVelocity);

            var targetSlot = initBlock.inputSlots.FirstOrDefault(o => o.property.type == attributeVelocity.outputSlots[0].property.type);
            attributeVelocity.outputSlots[0].Link(targetSlot);

            graph.vfxAsset = new VFXAsset();
            graph.RecompileIfNeeded();
            graph.vfxAsset.bounds = new Bounds(Vector3.zero, Vector3.positiveInfinity);

            var gameObj = new GameObject("CreateCustomSpawnerLinkedWithSourceAttribute");
            var vfxComponent = gameObj.AddComponent<VFXComponent>();
            vfxComponent.vfxAsset = graph.vfxAsset;

            int maxFrame = 512;
            while (vfxComponent.culled && --maxFrame > 0)
            {
                yield return null;
            }
            Assert.IsTrue(maxFrame > 0);
            yield return null; //wait for exactly one more update if visible

            var vfxEventAttribute = vfxComponent.CreateVFXEventAttribute();
            Assert.IsTrue(vfxEventAttribute.HasVector3("velocity"));
        }
        */
    }
}
