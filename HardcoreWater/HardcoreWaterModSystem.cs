using System.Reflection;
using HardcoreWater.ModBlock;
using HardcoreWater.ModBlockEntity;
using HardcoreWater.ModNetwork;
using HardcoreWater.ModPatches;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace HardcoreWater
{
    public class HardcoreWaterModSystem : ModSystem
    {
        private IServerNetworkChannel _serverChannel;
        private ICoreAPI _api;
        private Harmony _harmonyInst;

        public override void StartPre(ICoreAPI api)
        {
            const string cfgFileName = "HardcoreWater.json";

            try
            {
                HardcoreWaterConfig cfgFromDisk;
                if ((cfgFromDisk = api.LoadModConfig<HardcoreWaterConfig>(cfgFileName)) == null)
                {
                    api.StoreModConfig(HardcoreWaterConfig.Loaded, cfgFileName);
                }
                else
                {
                    HardcoreWaterConfig.Loaded = cfgFromDisk;
                }
            }
            catch
            {
                api.StoreModConfig(HardcoreWaterConfig.Loaded, cfgFileName);
            }

            base.StartPre(api);
        }

        public override void Start(ICoreAPI api)
        {
            _api = api;
            base.Start(api);

            api.RegisterBlockClass("BlockAqueduct", typeof(BlockAqueduct));
            api.RegisterBlockClass("BlockEnclosedAqueduct", typeof(BlockEnclosedAqueduct));
            api.RegisterBlockEntityClass("BlockEntityAqueduct", typeof(BlockEntityAqueduct));

            api.Logger.Notification("Loaded Hardcore Water!");
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            // Send connecting players config settings
            _serverChannel.SendPacket(
                new SyncConfigClientPacket
                {
                    AqueductUpdateFrequencySeconds = HardcoreWaterConfig
                        .Loaded
                        .AqueductUpdateFrequencySeconds,
                },
                player
            );
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            sapi.Event.PlayerJoin += OnPlayerJoin;

            if (!Harmony.HasAnyPatches(Mod.Info.ModID))
            {
                _harmonyInst = new Harmony(Mod.Info.ModID);

                PatchFindDownwardPaths(sapi, _harmonyInst);
                PatchTryLoweringLiquidLevel(sapi, _harmonyInst);
            }

            // Create server channel for config data sync
            _serverChannel = sapi
                .Network.RegisterChannel("hardcorewater")
                .RegisterMessageType<SyncConfigClientPacket>()
                .SetMessageHandler<SyncConfigClientPacket>((_, _) => { });

            base.StartServerSide(sapi);
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            // Sync config settings with clients
            capi.Network.RegisterChannel("hardcorewater")
                .RegisterMessageType<SyncConfigClientPacket>()
                .SetMessageHandler<SyncConfigClientPacket>(p =>
                {
                    Mod.Logger.Event("Received config settings from server");
                    HardcoreWaterConfig.Loaded.AqueductUpdateFrequencySeconds =
                        p.AqueductUpdateFrequencySeconds;
                });
        }

        public override void Dispose()
        {
            if (_api is ICoreServerAPI sapi)
            {
                sapi.Event.PlayerJoin -= OnPlayerJoin;
            }
        }

        private void PatchFindDownwardPaths(ICoreServerAPI sapi, Harmony harmony)
        {
            var original = typeof(BlockBehaviorFiniteSpreadingLiquid).GetMethod(
                "FindDownwardPaths",
                BindingFlags.Public | BindingFlags.Instance,
                [typeof(IWorldAccessor), typeof(BlockPos), typeof(Block)]
            );
            var postfix = typeof(PatchBlockBehaviorFiniteSpreadingLiquid).GetMethod(
                "PostfixFindDownwardPaths",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            harmony.Patch(original, null, new HarmonyMethod(postfix));

            sapi.Logger.Notification(
                "Applied patch to VintageStory's BlockBehaviorFiniteSpreadingLiquid.FindDownwardPaths from Hardcore Water!"
            );
        }

        private void PatchTryLoweringLiquidLevel(ICoreServerAPI sapi, Harmony harmony)
        {
            var original = typeof(BlockBehaviorFiniteSpreadingLiquid).GetMethod(
                "TryLoweringLiquidLevel",
                BindingFlags.NonPublic | BindingFlags.Instance,
                [typeof(Block), typeof(IWorldAccessor), typeof(BlockPos)]
            );
            var prefix = typeof(PatchBlockBehaviorFiniteSpreadingLiquid).GetMethod(
                "PrefixTryLoweringLiquidLevel",
                BindingFlags.NonPublic | BindingFlags.Static
            );

            harmony.Patch(original, new HarmonyMethod(prefix));

            sapi.Logger.Notification(
                "Applied patch to VintageStory's BlockBehaviorFiniteSpreadingLiquid.TryLoweringLiquidLevel from Hardcore Water!"
            );
        }
    }
}
