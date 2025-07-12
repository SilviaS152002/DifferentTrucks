using AssetPack.Runtime;
using Audio;
using HarmonyLib;
using Model;
using Model.Database;
using Railloader;
using RollingStock;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UI.Builder;
using UnityEngine;

namespace DifferentTrucks
{
    [HarmonyPatch(typeof(Car))]
    static partial class CarPatch
    {
        [HarmonyPatch(MethodType.Constructor), HarmonyPostfix]
        static void CarConstructorPostfix(Car __instance)
        {
            // Debug.Log("CarConstructorPostfix running");
            __instance.SetTruckPrefabLoadTasks(new List<Task<Wheelset>>());

        }

        private static readonly ConditionalWeakTable<Car, List<Task<Wheelset>>> TruckPrefabLoadTasksData = new ConditionalWeakTable<Car, List<Task<Wheelset>>>();
        public static bool TryGetTruckPrefabLoadTasks(this Car car, out List<Task<Wheelset>> truckPrefabLoadTasks) => TruckPrefabLoadTasksData.TryGetValue(car, out truckPrefabLoadTasks);
        public static void SetTruckPrefabLoadTasks(this Car car, List<Task<Wheelset>> truckPrefabLoadTasks)
        {
            TruckPrefabLoadTasksData.Remove(car);
            TruckPrefabLoadTasksData.Add(car, truckPrefabLoadTasks);
        }

        public static List<Task<Wheelset>> TruckPrefabForIdMultiple(this IPrefabStore prefabStore, string truckIdentifierMultiple)
        {
           // Debug.Log("TruckPrefabForIdMultiple running");
            string[] truckIdentifiers = truckIdentifierMultiple.Replace(", ", ",").Split(',');
           // Debug.Log("Split truckIdentifiers:" + String.Join(", ", truckIdentifiers));
            return truckIdentifiers.Select(truckIdentifier => prefabStore.TruckPrefabForId(truckIdentifier))/*.Where(task => task != null)*/.ToList();
        }

        /*
        public static async void LoadTrucksAsyncTask(this Car __instance)
        {
            throw new Exception("LoadTrucksAsyncTask: fuck everything");
           // Debug.Log("Is this a car? " + __instance);
            try
            {
                IPrefabStore prefabStore = TrainController.Shared.PrefabStore;
                if (!string.IsNullOrEmpty(__instance.Definition.TruckIdentifier))
                {
                    __instance.SetTruckPrefabLoadTasks(prefabStore.TruckPrefabForIdMultiple(__instance.Definition.TruckIdentifier));
                   // Debug.Log("SetTruckPrefabLoadTasks success");
                }
            }
            catch (Exception exception)
            {
               // Debug.LogError(exception + "SetTruckPrefabLoadTasks fail");
                return;
            }
            try
            {
                if (!TryGetTruckPrefabLoadTasks(__instance, out List<Task<Wheelset>> truckPrefabLoadTasks)) throw new Exception();
                await Task.WhenAll(truckPrefabLoadTasks);
               // Debug.Log("truckPrefabLoadTasks await success");
            }
            catch (Exception exception2)
            {
               // Debug.LogError(exception2 + "truckPrefabLoadTasks await fail");
                return;
            }
        }
        */

        [HarmonyPatch("LoadModelsAsync"), HarmonyPrefix] public static bool LoadModelsAsyncPrefix(Car __instance) => false; // Yeet the original code

        [HarmonyPatch("LoadModelsAsync"), HarmonyPostfix]
        public static async void LoadModelsAsyncPostfix(Car __instance)
        {
            List<Task<Wheelset>> truckPrefabs = [];
           // Debug.Log(__instance + " called LoadModelsAsync");
            try
            {
                IPrefabStore prefabStore = Traverse.Create(__instance).Property("TrainController").GetValue<TrainController>().PrefabStore;
                string modelIdentifier = __instance.Definition.ModelIdentifier;
                string assetPackIdentifier = prefabStore.AssetPackIdentifierContainingDefinition(__instance.DefinitionInfo.Identifier);
                Traverse.Create(__instance).Field("_modelLoadTasks").GetValue<Dictionary<string, Task<LoadedAssetReference<GameObject>>>>()["model"] = prefabStore.LoadAssetAsync<GameObject>(assetPackIdentifier, modelIdentifier, CancellationToken.None);
                if (!string.IsNullOrEmpty(__instance.Definition.TruckIdentifier))
                {
                   // Debug.Log("truckPrefabs to be loaded: " + __instance.Definition.TruckIdentifier);
                    truckPrefabs = prefabStore.TruckPrefabForIdMultiple(__instance.Definition.TruckIdentifier);
                   // Debug.Log("truckPrefabs count: " + truckPrefabs.Count);
                    __instance.SetTruckPrefabLoadTasks(truckPrefabs);
                    if (truckPrefabs == null)
                    {
                        throw new Exception();
                    }
                }
                await Task.WhenAll(Traverse.Create(__instance).Field("_modelLoadTasks").GetValue<Dictionary<string, Task<LoadedAssetReference<GameObject>>>>().Values);
            }
            catch (Exception)
            {

               // Debug.LogError("Error loading car model " + __instance.DefinitionInfo.Identifier);
            }
            try
            {
                if (truckPrefabs.Count > 0)
                {
                    await Task.WhenAll(truckPrefabs);
                   // Debug.Log("Loaded all truckPrefabs - Continuing");
                }
                else
                {
                   // Debug.Log("No TruckPrefabs - Skipping");
                }
            }
            catch (Exception)
            {
               // Debug.LogError("Error loading trucks");
            }
            // HandleModelsLoaded();
            AccessTools.Method(typeof(Car), "HandleModelsLoaded").Invoke(__instance, null);
        }


        /*
        [HarmonyPatch("LoadModelsAsync"), HarmonyPrefix]
        static bool LoadModelsAsyncPrefix(Car __instance)
        {
            return LoadModelsAsyncTask(__instance).Result;
        }

        
        static async Task<bool> LoadModelsAsyncTask(Car __instance)
        {
           // Debug.Log("LoadModelsAsyncPrefix patched");
            try
            {
                IPrefabStore prefabStore = TrainController.Shared.PrefabStore;
                string modelIdentifier = __instance.Definition.ModelIdentifier;
                string assetPackIdentifier = prefabStore.AssetPackIdentifierContainingDefinition(__instance.DefinitionInfo.Identifier);
                // __instance._modelLoadTasks["model"] = prefabStore.LoadAssetAsync<GameObject>(assetPackIdentifier, modelIdentifier, CancellationToken.None);
                // Dictionary<string, Task<LoadedAssetReference<GameObject>>> modelLoadTasks = (Dictionary<string, Task<LoadedAssetReference<GameObject>>>)Traverse.Create(__instance).Field("_modelLoadTasks").GetValue();
                ((Dictionary<string, Task<LoadedAssetReference<GameObject>>>)Traverse.Create(__instance).Field("_modelLoadTasks").GetValue())["model"] = prefabStore.LoadAssetAsync<GameObject>(assetPackIdentifier, modelIdentifier, CancellationToken.None);
                // Traverse.Create(__instance).Field("_modelLoadTasks").SetValue(modelLoadTasks);
                if (!string.IsNullOrEmpty(__instance.Definition.TruckIdentifier))
                {
                    __instance.SetTruckPrefabLoadTasks(prefabStore.TruckPrefabForIdMultiple(__instance.Definition.TruckIdentifier));
                   // Debug.Log("SetTruckPrefabLoadTasks success");
                }
                await Task.WhenAll(((Dictionary<string, Task<LoadedAssetReference<GameObject>>>)Traverse.Create(__instance).Field("_modelLoadTasks").GetValue()).Values);
               // Debug.Log("_modelLoadTasks success");
            }
            catch (Exception exception)
            {
               // Debug.LogError(exception, "Error loading car model {identifier}", __instance.DefinitionInfo.Identifier);
                return false;
            }
            try
            {
                if (!TryGetTruckPrefabLoadTasks(__instance, out List<Task<Wheelset>> truckPrefabLoadTasks)) throw new Exception();
                await Task.WhenAll(truckPrefabLoadTasks);
               // Debug.Log("truckPrefabLoadTasks await success");
            }
            catch (Exception exception2)
            {
               // Debug.LogError(exception2, "Error loading trucks");
                return false;
            }
           // Debug.Log("Entering HandleModelsLoaded");
            Traverse.Create(__instance).Method("HandleModelsLoaded");
            return false;
        }
        */




        [HarmonyPatch("SetupTrucks"), HarmonyPrefix]
        static bool SetupTrucksPrefix(Car __instance)
        {
           // Debug.Log("SetupTrucksPrefix loaded");
            if (string.IsNullOrEmpty(__instance.Definition.TruckIdentifier) || (!TryGetTruckPrefabLoadTasks(__instance, out List<Task<Wheelset>> truckPrefabLoadTasks)))
            {
               // Debug.Log("Well this isn't right");
                return false;

            }
           // Debug.Log("truckPrefabLoadTasks count: " + truckPrefabLoadTasks.Count);
            if (truckPrefabLoadTasks.Count <= 0) throw new ArgumentOutOfRangeException("There should be at least one something here");
            foreach (var wheelset in truckPrefabLoadTasks)
            {
               // Debug.Log("Wheelset: " + wheelset.Result);
            }

            List<Wheelset> Wheelsets = truckPrefabLoadTasks.Select(wheelset => wheelset.Result).ToList();
           // Debug.Log("Wheelset number pre-ops: " + Wheelsets.Count);
            if (Wheelsets.Count <= 0)
            {
               // Debug.LogWarning("Car defines trucks " + __instance.Definition.TruckIdentifier + " but task has no result");
                return false;
            }

            if (Wheelsets.Count == 1)
            {
                Wheelsets.Add(Wheelsets[0]);
            }
           // Debug.Log("Wheelset number post-ops: " + Wheelsets.Count);


            WheelClackProfile wheelClackProfile = TrainController.Shared.wheelClackProfile;
            var linearOffset = Traverse.Create(__instance).Field("_linearOffset").GetValue<float>();
            float truckDistFromCenter = __instance.truckSeparation / 2f;
            List<Renderer[]> renderersList = new List<Renderer[]>();

            for (int truck_id = 0; truck_id < Wheelsets.Count; truck_id++)
            {
                Wheelset Truck = UnityEngine.Object.Instantiate(Wheelsets[truck_id], __instance.BodyTransform, worldPositionStays: false);
                Truck.name = "Truck " + truck_id;
                Truck.Configure(wheelClackProfile, __instance);
                Truck.SetLinearOffset(linearOffset - truckDistFromCenter);
                Truck.transform.localPosition = Vector3.forward * truckDistFromCenter;
                Truck.transform.localRotation = Quaternion.identity;
                Traverse.Create(__instance).Field("BrakeAnimators").GetValue<HashSet<IBrakeAnimator>>().Add(Truck);
                renderersList.Add((Renderer[])AccessTools.Method(typeof(Car), "GetRenderers").Invoke(__instance, [Truck.gameObject]));
                AccessTools.Method(typeof(Car), "MakeMaterialsUnique").Invoke(__instance, [Truck.gameObject, (IReadOnlyCollection<Renderer>)(object)renderersList[truck_id]]);
                Wheelsets[truck_id] = Truck;
            }
            // Move below into for loop for multi bogie support
            if (__instance.EnableOiling)
            {
                float diameter = Traverse.Create(Wheelsets[0]).Field("diameterInInches").GetValue<float>() / 39.37008f;
                float axleSeparation = Wheelsets[0].CalculateAxleSpread();
                AccessTools.Method(typeof(Car), "AddOilPointPickable").Invoke(__instance, [truckDistFromCenter, axleSeparation, diameter]);
                AccessTools.Method(typeof(Car), "AddOilPointPickable").Invoke(__instance, [-truckDistFromCenter, axleSeparation, diameter]);
            }
            Traverse.Create(__instance).Field("_truckA").SetValue(Wheelsets[0]);
            // Insert code for slapping on other wheelsets
            Wheelsets[Wheelsets.Count - 1].SetLinearOffset(linearOffset - -truckDistFromCenter);
            Wheelsets[Wheelsets.Count - 1].transform.localPosition = Vector3.forward * -truckDistFromCenter;
            Traverse.Create(__instance).Field("_truckB").SetValue(Wheelsets[Wheelsets.Count - 1]);
            Traverse.Create(__instance).Field("_truckRenderers").GetValue<List<Renderer>>().AddRange(renderersList.SelectMany(renderers => renderers).Where(array => array != null));
            return false;
        }

        [HarmonyPatch("HandleModelsLoaded"), HarmonyPrefix]
        static bool HandleModelsLoadedPrefix(Car __instance)
        {
           // Debug.Log(__instance + " called HandleModelsLoaded");
            return true;
        }

        /*

        [HarmonyPatch(typeof(IPrefabStore), "LoadWheelset", [typeof(string), typeof(TaskCompletionSource<Wheelset>)]), HarmonyPrefix]
        static async Task<bool> LoadWheelsetPrefix(IPrefabStore __instance)
        {

        }
        */


        public class DifferentTrucksPlugin : SingletonPluginBase<DifferentTrucksPlugin>, IModTabHandler
        {
            public DifferentTrucksPlugin() => new Harmony("DifferentTrucksPlugin").PatchAll();
            public void ModTabDidClose() { }
            public void ModTabDidOpen(UIPanelBuilder builder) { }
        }
    }

    [HarmonyPatch(typeof(Wheelset))]
    public class WheelsetPatch
    {

        [HarmonyPatch("Roll"), HarmonyPrefix]
        static bool RollPrefix(Wheelset __instance)
        {
           // Debug.Log(__instance + " called Roll");
           // Debug.Log("Transforms: "+((List<Transform>)Traverse.Create(__instance).Field("wheels").GetValue()).Count());
            return true;
        }

    }
}
