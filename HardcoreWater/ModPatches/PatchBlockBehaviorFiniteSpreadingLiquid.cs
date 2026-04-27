using System.Collections.Generic;
using HardcoreWater.ModBlock;
using HardcoreWater.ModBlockEntity;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace HardcoreWater.ModPatches
{
    public class PatchBlockBehaviorFiniteSpreadingLiquid
    {
        // ReSharper disable once UnusedMember.Local
        [HarmonyPatch(typeof(BlockBehaviorFiniteSpreadingLiquid), "FindDownwardPaths")]
        [HarmonyPostfix]
        static void PostfixFindDownwardPaths(
            // ReSharper disable once InconsistentNaming
            BlockBehaviorFiniteSpreadingLiquid __instance,
            // ReSharper disable once InconsistentNaming
            ref List<PosAndDist> __result,
            IWorldAccessor world,
            BlockPos pos,
            Block ourBlock
        )
        {
            _ = __instance;
            _ = ourBlock;

            if (__result == null)
            {
                return;
            }

            // If solid block of water is aqueduct, add aqueduct directions to valid downward paths
            if (
                world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Solid)
                is not BlockAqueduct blockAqueduct
            )
            {
                return;
            }

            // Scan blocks front and back of the aqueduct
            if (BlockFacing.FromFirstLetter(blockAqueduct.Orientation) == BlockFacing.NORTH)
            {
                __result.Add(new PosAndDist { pos = pos.NorthCopy(), dist = 1 });
                __result.Add(new PosAndDist { pos = pos.SouthCopy(), dist = 1 });
            }
            else
            {
                __result.Add(new PosAndDist { pos = pos.WestCopy(), dist = 1 });
                __result.Add(new PosAndDist { pos = pos.EastCopy(), dist = 1 });
            }
        }

        // ReSharper disable once UnusedMember.Local
        [HarmonyPatch(typeof(BlockBehaviorFiniteSpreadingLiquid), "TryLoweringLiquidLevel")]
        [HarmonyPrefix]
        static bool PrefixTryLoweringLiquidLevel(
            // ReSharper disable once InconsistentNaming
            BlockBehaviorFiniteSpreadingLiquid __instance,
            // ReSharper disable once InconsistentNaming
            ref bool __result,
            Block ourBlock,
            IWorldAccessor world,
            BlockPos pos
        )
        {
            _ = __instance;

            var ourSolid = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Solid);

            if (
                ourSolid is not IAqueduct
                || ourBlock.GetBlockEntity<BlockEntityAqueduct>(pos) is not { } blockEntityAqueduct
                // Add check to ignore liquid levels of 1, based on reports from Chronolegionnaire
                || ourBlock.LiquidLevel == 1
                || ourBlock.LiquidLevel - 1 > blockEntityAqueduct.WaterLevel
                || !blockEntityAqueduct.HasWaterSource
            )
            {
                return true; // resume original method
            }

            __result = false;
            return false; // skip original method
        }
    }
}
