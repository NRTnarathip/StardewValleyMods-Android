using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        private readonly object SerializerInitializationLock = new();

        private Task SerializerInitializationTask;

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

        public bool HasCustomSerializers => SpaceCore.ModTypes.Count > 0 || this.HasPyTk;

        public void RunTaskInitializeSerializers()
        {
            if (!this.HasCustomSerializers || this.SerializerInitializationTask != null)
                return;

            lock (this.SerializerInitializationLock)
            {
                if (this.SerializerInitializationTask != null)
                    return;

                Log.Trace($"Reinitializing serializers for {SpaceCore.ModTypes.Count} mod types...");
                this.SerializerInitializationTask = Task.WhenAll(
                    Task.Run(() => this.InitializeSerializer(typeof(SaveGame), this.VanillaMainTypes)),
                    Task.Run(() => this.InitializeSerializer(typeof(Farmer), this.VanillaFarmerTypes)),
                    Task.Run(() => this.InitializeSerializer(typeof(GameLocation), this.VanillaGameLocationTypes)),
                    Task.Run(() => this.InitializeSerializer(typeof(DescriptionElement), this.VanillaLegacyDescriptionElementTypes))
                );
            }
        }

        public void InitializeSerializers()
        {
            // skip if already initialized
            if (this.InitializedSerializers)
                return;

            if (!this.HasCustomSerializers)
            {
                this.InitializedSerializers = true;
                return;
            }

            this.RunTaskInitializeSerializers();
            this.SerializerInitializationTask.GetAwaiter().GetResult();

            // done
            this.InitializedSerializers = true;
            Game1.otherFarmers.Serializer = SaveSerializer.GetSerializer(typeof(Farmer));
        }

        private readonly ConcurrentDictionary<Type, Lazy<XmlSerializer>> Serializers = new();

        private readonly object NotifyPyTKLock = new();

        public XmlSerializer InitializeSerializer(Type baseType, Type[] extra = null)
        {
            Type[] knownTypes = (extra ?? this.GetVanillaTypes(baseType))
                .Concat(SpaceCore.ModTypes)
                .Distinct()
                .ToArray();

            return this.Serializers.GetOrAdd(
                baseType,
                _ => new Lazy<XmlSerializer>(
                    () => this.CreateSerializer(baseType, knownTypes),
                    LazyThreadSafetyMode.ExecutionAndPublication
                )
            ).Value;
        }


        /*********
        ** Private methods
        *********/
        private XmlSerializer CreateSerializer(Type baseType, Type[] knownTypes)
        {
            XmlSerializer serializer = new(baseType, knownTypes);
            lock (this.NotifyPyTKLock)
                this.NotifyPyTk(serializer);
            return serializer;
        }

        private Type[] GetVanillaTypes(Type baseType)
        {
            if (baseType == typeof(SaveGame))
                return this.VanillaMainTypes;
            if (baseType == typeof(Farmer))
                return this.VanillaFarmerTypes;
            if (baseType == typeof(GameLocation))
                return this.VanillaGameLocationTypes;
            if (baseType == typeof(DescriptionElement))
                return this.VanillaLegacyDescriptionElementTypes;
            return Array.Empty<Type>();
        }

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
