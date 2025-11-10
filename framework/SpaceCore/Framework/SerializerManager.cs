using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using SpaceCore.Patches;
using SpaceShared;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.Quests;
using StardewValley.SaveSerialization;
using StardewValley.TerrainFeatures;

namespace SpaceCore.Framework
{
    /// <summary>Manages the serialization for <see cref="LoadGameMenuPatcher"/> and <see cref="SaveGamePatcher"/>.</summary>
    internal class SerializerManager
    {
        /*********
        ** Fields
        *********/
        /// <summary>Whether PyTK is installed.</summary>
        private readonly bool HasPyTk;

        /// <summary>Whether SpaceCore's custom types have been added to the <see cref="SaveGame"/> serializers.</summary>
        private bool InitializedSerializers;

        // Update these each game update
        private readonly Type[] VanillaMainTypes =
        {
            typeof(Character),
            typeof(GameLocation),
            typeof(Item),
            typeof(Quest),
            typeof(TerrainFeature)
        };
        private readonly Type[] VanillaFarmerTypes =
        {
            typeof(Item)
        };
        private readonly Type[] VanillaGameLocationTypes =
        {
            typeof(Character),
            typeof(Item),
            typeof(TerrainFeature)
        };
        private readonly Type[] VanillaDescriptionElementTypes =
        {
            typeof(Character),
            typeof(Item)
        };
        private readonly Type[] VanillaLegacyDescriptionElementTypes =
        {
            typeof(DescriptionElement),
            typeof(Character),
            typeof(Item)
        };


        /*********
        ** Accessors
        *********/
        public readonly string Filename = "spacecore-serialization.json";
        public readonly string FarmerFilename = "spacecore-serialization-farmer.json";

        /// <summary>The save filename currently being loaded, if any.</summary>
        public string LoadFileContext = null;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="modRegistry">The mod registry to check for installed mods.</param>
        public SerializerManager(IModRegistry modRegistry)
        {
            this.HasPyTk = modRegistry.IsLoaded("Platonymous.Toolkit");
        }

        Task? m_taskInitializeSerializers;
        public void RunTaskInitializeSerializers()
        {
            if (m_taskInitializeSerializers == null
                && (SpaceCore.ModTypes.Any() || this.HasPyTk))
            {
                Log.Trace($"Reinitializing serializers for {SpaceCore.ModTypes.Count} mod types...");
                m_taskInitializeSerializers = Task.Run(() =>
                {
                    var task1 = Task.Run(() =>
                    {
                        InitializeSerializer(typeof(SaveGame), this.VanillaMainTypes);
                        Log.Info("done init SaveGame.serializer ");
                    });
                    var task2 = Task.Run(() =>
                    {
                        InitializeSerializer(typeof(Farmer), this.VanillaFarmerTypes);
                        Log.Info("done init SaveGame.farmerSerializer ");
                    });
                    var task3 = Task.Run(() =>
                    {
                        InitializeSerializer(typeof(GameLocation), this.VanillaGameLocationTypes);
                        Log.Info("done init SaveGame.locationSerializer ");
                    });
                    var task4 = Task.Run(() =>
                    {
                        InitializeSerializer(typeof(DescriptionElement), this.VanillaDescriptionElementTypes);
                        Log.Info("done init SaveGame.descriptionElementSerializer ");
                    });
                    var task5 = Task.Run(() =>
                    {
                        InitializeSerializer(typeof(DescriptionElement), this.VanillaLegacyDescriptionElementTypes);
                        Log.Info("done init SaveGame.legacyDescriptionElementSerializer ");
                    });
                    Task.WaitAll(task1, task2, task3, task4, task5);
                });
            }
        }
        public void InitializeSerializers()
        {
            // skip if already initialized
            if (this.InitializedSerializers)
                return;

            // waiting for task
            m_taskInitializeSerializers.Wait();

            // done
            this.InitializedSerializers = true;
            Game1.otherFarmers.Serializer = SaveSerializer.GetSerializer(typeof(Farmer));
        }

        private ConcurrentDictionary<Type, XmlSerializer> serializersAlreadyDone = new();

        object NotifyPyTK_Lock = new object();
        public XmlSerializer InitializeSerializer(Type baseType, Type[] extra = null)
        {
            //Console.WriteLine($"on getting InitializeSerializer: baseType: {baseType}");
            if (serializersAlreadyDone.TryGetValue(baseType, out var tryGetValue))
                return tryGetValue;

            var types = extra?.Length > 0
                ? extra.Concat(SpaceCore.ModTypes)
                : SpaceCore.ModTypes;

            XmlSerializer serializer = new(baseType, types.ToArray());
            serializersAlreadyDone.TryAdd(baseType, serializer);
            lock (NotifyPyTK_Lock)
                this.NotifyPyTk(serializer);
            return serializer;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Notify PyTK that the serializers were changed, if it's installed.</summary>
        /// <param name="serializer">The XML serializer which changed.</param>
        private void NotifyPyTk(XmlSerializer serializer)
        {
            if (!this.HasPyTk)
                return;

            const string errorPrefix = "PyTK is installed, but we couldn't notify it about serializer changes. PyTK serialization might not work correctly.\nTechnical details:";
            try
            {
                // fetch PyTK mod
                var mod = Type.GetType("PyTK.PyTKMod, PyTK");
                if (mod == null)
                {
                    Log.Monitor.LogOnce($"{errorPrefix} couldn't fetch its mod instance.");
                    return;
                }

                // fetch notify method
                const string methodName = "SerializersReinitialized";
                var method = mod.GetMethod(methodName);
                if (method == null)
                {
                    Log.Monitor.LogOnce($"{errorPrefix} couldn't fetch its '{methodName}' method.");
                    return;
                }

                // notify
                method.Invoke(null, new object[] { serializer });
            }
            catch (Exception ex)
            {
                Log.Monitor.LogOnce($"{errorPrefix} {ex}", LogLevel.Warn);
            }
        }
    }
}
