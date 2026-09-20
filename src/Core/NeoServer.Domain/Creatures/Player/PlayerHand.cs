using NeoServer.Domain.Common;
using NeoServer.Domain.Common.Contracts;
using NeoServer.Domain.Common.Contracts.Creatures;
using NeoServer.Domain.Common.Contracts.Items;
using NeoServer.Domain.Common.Contracts.Items.Types;
using NeoServer.Domain.Common.Contracts.World.Tiles;
using NeoServer.Domain.Common.Creatures.Structs;
using NeoServer.Domain.Common.Results;
using NeoServer.Domain.Items.Services;

namespace NeoServer.Domain.Creatures.Player;

public class PlayerHand
{
    private readonly IPlayer _player;

    public PlayerHand(IPlayer player)
    {
        _player = player;
    }

    public Result<OperationResultList<IItem>> Move(IItem item, IHasItem from, IHasItem destination, byte amount,
        byte fromPosition, byte? toPosition)
    {
        if (!item.CanBeMoved) return Result<OperationResultList<IItem>>.NotPossible;

        if (!item.IsCloseTo(_player)) return new Result<OperationResultList<IItem>>(InvalidOperation.TooFar);

        var movement = new ItemMovementContext(_player, item, from, destination, amount, fromPosition, toPosition);
        return ItemTransferOperation.Execute(movement);
    }

    public Result<OperationResultList<IItem>> PickItemFromGround(IItem item, ITile tile, byte amount = 1)
    {
        if (tile is not IDynamicTile fromTile) return Result<OperationResultList<IItem>>.NotPossible;

        var topItemOnStackIsPickupable = tile.TopDownItemOnStack?.IsPickupable ?? false;

        if (!topItemOnStackIsPickupable) return Result<OperationResultList<IItem>>.NotPossible;
        if (_player.Inventory.BackpackSlot is not { } backpack) return Result<OperationResultList<IItem>>.NotPossible;

        if (tile.TopDownItemOnStack != item) return Result<OperationResultList<IItem>>.NotPossible;

        return Move(tile.TopDownItemOnStack, fromTile, backpack, amount, 0, 0);
    }

}
