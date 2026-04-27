using System;
using HardcoreWater.ModBlock;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace HardcoreWater.ModBlockEntity
{
    public class BlockEntityAqueduct : BlockEntity
    {
        private bool IsValidWaterSourceOrWaterFall(BlockPos blockPos, int minLevel = 7)
        {
            var isValid = IsValidWaterSource(blockPos, minLevel) || IsValidWaterFall(blockPos);
            return isValid;
        }

        private bool IsValidWaterSource(BlockPos blockPos, int minLevel = 7)
        {
            var block = Api.World.BlockAccessor.GetBlock(blockPos, BlockLayersAccess.Fluid);

            return block switch
            {
                BlockWaterflowing blockWaterflowing => (blockWaterflowing.LiquidLevel >= minLevel),
                BlockWater blockWater => blockWater.LiquidLevel >= minLevel,
                _ => false,
            };
        }

        private bool IsValidWaterFall(BlockPos blockPos)
        {
            var block = Api.World.BlockAccessor.GetBlock(blockPos, BlockLayersAccess.Fluid);

            if (block is BlockWaterfall blockWaterfall)
            {
                return (blockWaterfall.LiquidLevel >= 6 && blockWaterfall.Variant["flow"] == "d");
            }

            return false;
        }

        private bool IsValidFilledAqueduct(BlockPos blockPos)
        {
            if (Api.World.BlockAccessor.GetBlock(blockPos) is not IAqueduct aqueduct)
            {
                return false;
            }

            if (
                Api.World.BlockAccessor.GetBlockEntity<BlockEntityAqueduct>(blockPos)
                is not { } adjacentAqueduct
            )
            {
                // Sometimes block entity will return null while block is aqueduct; assume valid source
                return true;
            }

            // To be a valid source aqueduct for this one, the adjacent aqueduct must be oriented in the same direction OR not enclosed
            var correctOrientation =
                aqueduct.Orientation == _blockAqueduct.Orientation || !aqueduct.IsEnclosed;
            var notSourcingThis = adjacentAqueduct.WaterSourcePos != WaterSourcePos;
            var notSourcingEachOther =
                (WaterSourcePos == null || adjacentAqueduct.WaterSourcePos == null)
                || !(
                    adjacentAqueduct.WaterSourcePos == Pos && WaterSourcePos == adjacentAqueduct.Pos
                );
            var hasMinWater = adjacentAqueduct.HasWaterSource;
            var isValid =
                correctOrientation && notSourcingThis && notSourcingEachOther && hasMinWater;

            return isValid;
        }

        private bool HasInvalidSourceDependency(BlockPos posA, BlockPos posB)
        {
            // Check for case that aqueduct is valid source for two adjacent aqueducts, invalidate if so
            if (
                Api.World.BlockAccessor.GetBlockEntity(posA)
                    is not BlockEntityAqueduct entityAqueductA
                || Api.World.BlockAccessor.GetBlockEntity(posB)
                    is not BlockEntityAqueduct entityAqueductB
            )
            {
                return false;
            }

            var sourcesBothAdjacent =
                entityAqueductA.WaterSourcePos == Pos && entityAqueductB.WaterSourcePos == Pos;
            var sourcedFromEitherAdjacent =
                WaterSourcePos == entityAqueductA.Pos || WaterSourcePos == entityAqueductB.Pos;

            return sourcesBothAdjacent && sourcedFromEitherAdjacent;
        }

        private bool DoesBlockBelowPosHaveUpSolidFaceOrAqueduct(BlockPos blockPos)
        {
            var mostSolidBlock = Api.World.BlockAccessor.GetMostSolidBlock(blockPos.DownCopy());

            if (mostSolidBlock is BlockAqueduct)
            {
                return true;
            }

            var isSolidTop =
                mostSolidBlock.GetLiquidBarrierHeightOnSide(BlockFacing.UP, blockPos.DownCopy())
                >= 1.0;

            return isSolidTop;
        }

        private void OnServerTick1S(float dt)
        {
            if (_blockAqueduct == null)
                return;

            var blockPosFb =
                BlockFacing.FromFirstLetter(_blockAqueduct.Orientation) == BlockFacing.NORTH
                    ? new[] { Pos.NorthCopy(), Pos.SouthCopy() }
                    : new[] { Pos.WestCopy(), Pos.EastCopy() };

            // Check validity of previous source location, if present
            if (HasWaterSource)
            {
                var hasSource = false;
                var unloadedWaterSource =
                    Api.World.BlockAccessor.GetChunkAtBlockPos(WaterSourcePos) == null;

                if (IsValidWaterSource(Pos) || unloadedWaterSource)
                {
                    hasSource = true; // Contains source block or source block is in unloaded chunk
                }
                else if (
                    IsValidWaterSource(WaterSourcePos)
                    && DoesBlockBelowPosHaveUpSolidFaceOrAqueduct(WaterSourcePos)
                )
                {
                    hasSource = true; // Connected to source block or source block is in unloaded chunk
                }
                else if (
                    IsValidWaterFall(WaterSourcePos)
                    && DoesBlockBelowPosHaveUpSolidFaceOrAqueduct(WaterSourcePos)
                )
                {
                    hasSource = true; // Connected to waterfall or source block is in unloaded chunk
                }
                else if (
                    IsValidWaterSource(WaterSourcePos, 5)
                    && IsValidWaterSourceOrWaterFall(WaterSourcePos.UpCopy(), 5)
                    && DoesBlockBelowPosHaveUpSolidFaceOrAqueduct(WaterSourcePos)
                )
                {
                    hasSource = true; // Connected to waterfall adjacent with above flowing water or source block is in unloaded chunk
                }
                else if (
                    IsValidWaterSource(Pos, 6)
                    && IsValidWaterSourceOrWaterFall(WaterSourcePos, 6)
                    && Pos.Y == WaterSourcePos.Y - 1
                )
                {
                    hasSource = true; // Connected to waterfall above or source block is in unloaded chunk
                }
                else if (IsValidFilledAqueduct(WaterSourcePos))
                {
                    hasSource = true; // Connected to aqueduct that isn't using this one as a source and has valid water source or source block is in unloaded chunk
                }

                if (hasSource && !HasInvalidSourceDependency(blockPosFb[0], blockPosFb[1]))
                {
                    return;
                }

                _waterSourceReacquireTimeout = 4;
                HasWaterSource = false;
                WaterSourcePos = null;
            }
            else
            {
                if (_waterSourceReacquireTimeout > 0)
                {
                    --_waterSourceReacquireTimeout;
                    WaterLevel = Math.Max(0, WaterLevel - 1);
                    Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
                    MarkDirty(true);
                    return;
                }

                var hasSource = false;
                var upwardPos = Pos.UpCopy();

                if (IsValidWaterSource(Pos))
                {
                    WaterSourcePos = Pos;
                    WaterLevel = 7;
                    hasSource = true;
                    HasWaterSource = true;
                    // Connected to source block in aqueduct
                }
                else if (IsValidWaterSource(upwardPos))
                {
                    WaterSourcePos = upwardPos;
                    WaterLevel = 6;
                    hasSource = true;
                    HasWaterSource = true;
                    // Connected to source block above
                }
                else if (IsValidWaterSourceOrWaterFall(upwardPos, 6))
                {
                    WaterSourcePos = upwardPos;
                    WaterLevel = 6;
                    hasSource = true;
                    HasWaterSource = true;
                    // Connected to waterfall or water above
                }
                else if (IsValidFilledAqueduct(upwardPos))
                {
                    WaterSourcePos = upwardPos;
                    WaterLevel = 6;
                    hasSource = true;
                    HasWaterSource = true;
                    // Connected to aqueduct above
                }

                if (!hasSource) // Check ends if source above
                {
                    foreach (BlockPos endPos in blockPosFb)
                    {
                        if (IsValidWaterSource(endPos))
                        {
                            WaterSourcePos = endPos;
                            WaterLevel = 6;
                            hasSource = true;
                            HasWaterSource = true;
                            break; // Connected to source block adjacent
                        }

                        if (
                            IsValidWaterFall(endPos)
                            && DoesBlockBelowPosHaveUpSolidFaceOrAqueduct(endPos)
                        )
                        {
                            WaterSourcePos = endPos;
                            WaterLevel = 6;
                            hasSource = true;
                            HasWaterSource = true;
                            break; // Connected to waterfall adjacent
                        }

                        if (
                            IsValidWaterSource(endPos, 5)
                            && IsValidWaterSourceOrWaterFall(endPos.UpCopy(), 5)
                            && DoesBlockBelowPosHaveUpSolidFaceOrAqueduct(endPos)
                        )
                        {
                            WaterSourcePos = endPos;
                            WaterLevel = 6;
                            hasSource = true;
                            HasWaterSource = true;
                            break; // Connected to waterfall adjacent with above flowing water
                        }

                        if (IsValidFilledAqueduct(endPos))
                        {
                            WaterSourcePos = endPos;
                            WaterLevel = 6;
                            hasSource = true;
                            HasWaterSource = true;
                            break; // Connected to aqueduct that isn't using this one as a source and has valid water source adjacent
                        }
                    }
                }

                if (hasSource)
                {
                    // Handle fresh, salt, and boiling water separately, lest we desalinate or something else weird
                    var ourBlockFluid = Api.World.BlockAccessor.GetBlock(
                        Pos,
                        BlockLayersAccess.Fluid
                    );
                    var sourceBlockFluid = Api.World.BlockAccessor.GetBlock(
                        WaterSourcePos,
                        BlockLayersAccess.Fluid
                    );
                    Block liquidBlockToSet;

                    if (sourceBlockFluid.Code.BeginsWith("game", "salt"))
                    {
                        liquidBlockToSet = Api.World.GetBlock(
                            new AssetLocation("game:saltwater-still-" + Math.Min(7, WaterLevel))
                        );
                    }
                    else if (sourceBlockFluid.Code.BeginsWith("game", "boiling"))
                    {
                        liquidBlockToSet = Api.World.GetBlock(
                            new AssetLocation("game:boilingwater-still-" + Math.Min(7, WaterLevel))
                        );
                    }
                    else if (
                        sourceBlockFluid.Code.BeginsWith("game", "rapidwater")
                        && _canTransportRapids
                    )
                    {
                        liquidBlockToSet = Api.World.GetBlock(
                            new AssetLocation("game:rapidwater-still-" + Math.Min(7, WaterLevel))
                        );
                    }
                    else
                    {
                        liquidBlockToSet = Api.World.GetBlock(
                            new AssetLocation("game:water-still-" + Math.Min(7, WaterLevel))
                        );
                    }

                    var iced = ourBlockFluid.Code.Path.Contains("ice");

                    if (
                        iced
                        || ourBlockFluid.LiquidLevel >= WaterLevel
                        || HasInvalidSourceDependency(blockPosFb[0], blockPosFb[1])
                    )
                    {
                        return;
                    }

                    Api.World.BlockAccessor.SetBlock(
                        liquidBlockToSet!.BlockId,
                        Pos,
                        BlockLayersAccess.Fluid
                    );
                }
                else
                {
                    WaterLevel = Math.Max(0, WaterLevel - 1);
                }

                Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
            }

            MarkDirty(true);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _blockAqueduct = (Block as IAqueduct);
            _canTransportRapids = HardcoreWaterConfig.Loaded.CanTransportRapids;
            RegisterGameTickListener(
                OnServerTick1S,
                (int)Math.Round(HardcoreWaterConfig.Loaded.AqueductUpdateFrequencySeconds * 1000)
            );
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("WaterLevel", WaterLevel);
            tree.SetInt("WaterSourceReacquireTimeout", _waterSourceReacquireTimeout);
            tree.SetBool("HasWaterSource", HasWaterSource);

            if (HasWaterSource)
            {
                tree.SetBlockPos("WaterSourcePos", WaterSourcePos);
            }
        }

        public override void FromTreeAttributes(
            ITreeAttribute tree,
            IWorldAccessor worldAccessForResolve
        )
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            WaterLevel = tree.GetInt("WaterLevel");
            _waterSourceReacquireTimeout = tree.GetInt("WaterSourceReacquireTimeout");
            HasWaterSource = tree.GetBool("HasWaterSource", true);
            WaterSourcePos = tree.GetBlockPos("WaterSourcePos");
        }

        private IAqueduct _blockAqueduct;

        public int WaterLevel { get; private set; }

        public BlockPos WaterSourcePos { get; private set; }

        public bool HasWaterSource { get; private set; }

        private int _waterSourceReacquireTimeout;

        private bool _canTransportRapids;
    }
}
