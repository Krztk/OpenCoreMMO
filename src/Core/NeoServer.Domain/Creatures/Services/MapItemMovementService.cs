using NeoServer.Domain.Common;
using NeoServer.Domain.Common.Contracts;
using NeoServer.Domain.Common.Contracts.Creatures;
using NeoServer.Domain.Common.Contracts.Items;
using NeoServer.Domain.Common.Contracts.Items.Types;
using NeoServer.Domain.Common.Contracts.Services;
using NeoServer.Domain.Common.Contracts.World;
using NeoServer.Domain.Common.Contracts.World.Tiles;
using NeoServer.Domain.Common.Creatures.Structs;
using NeoServer.Domain.Common.Location;
using NeoServer.Domain.Common.Results;
using NeoServer.Domain.Common.Services;
using NeoServer.Domain.Common.Texts;
using NeoServer.Domain.Items.Services;
using NeoServer.Domain.Mail;
using NeoServer.Domain.World.Events;
using Location = NeoServer.Domain.Common.Location.Structs.Location;

namespace NeoServer.Domain.Creatures.Services;

/// <summary>
///     Service that handles item movement between map tiles.
///     Executes a single, consistent pipeline:
///     <list type="number">
///         <item>Throw validation (distance, line-of-sight, special tile exemptions)</item>
///         <item>Walk-to mechanism (if the source item is too far from the player)</item>
///         <item>Destination resolution (teleports, holes, floor changes)</item>
///         <item>Core item transfer (remove from the source tile, add to the destination tile)</item>
///     </list>
/// </summary>
public class MapItemMovementService(
    IMap map,
    IWalkToMechanism walkToMechanism,
    IItemThrowValidator itemThrowValidator,
    IMailService mailService) : IMapItemMovementService
{
    /// <summary>
    ///     Moves an item from the player's inventory or an open container to a map tile,
    ///     performing throw validation (from the player's current position) and destination resolution.
    /// </summary>
    public Result<OperationResultList<IItem>> Move(IPlayer player, IItem item, IHasItem from,
        ITile destination, byte amount, byte fromPosition, byte? toPosition)
    {
        if (player is null || item is null || !item.CanBeMoved)
            return Result<OperationResultList<IItem>>.NotPossible;

        // --- Throw validation from the player's location (item is in inventory, not on the ground) ---
        var throwValidation = itemThrowValidator.Validate(player, player.Location, destination.Location, destination);
        if (throwValidation.Failed)
        {
            // If destination is out of reach, walk toward it and retry.
            if (throwValidation.Reason == InvalidOperation.TooFar)
            {
                walkToMechanism.WalkTo(player,
                    () => Move(player, item, from, destination, amount, fromPosition, toPosition),
                    destination.Location);
                return Result<OperationResultList<IItem>>.Success;
            }

            SendThrowError(player, throwValidation.Reason);
            return new Result<OperationResultList<IItem>>(throwValidation.Reason);
        }

        // --- Resolve destination tile (teleports, holes, floor changes) ---
        destination = ResolveDestination(destination);

        // --- Trash holder / liquid source handling ---
        if (destination.HasFlag(TileFlags.TrashHolder))
        {
            EventAggregator.Invoke(new ItemMovedToTrashHolder(item, destination));
            return ConsumeItem(item, from, amount, fromPosition);
        }

        // --- Mail box handling ---
        if (destination.HasFlag(TileFlags.MailBox) && destination is IDynamicTile mailBoxTile)
        {
            if (!item.IsMailable)
            {
                OperationFailService.Send(player, InvalidOperation.NotPossible);
                return Result<OperationResultList<IItem>>.NotPossible;
            }

            return HandleMailBoxMove(player, item, from, mailBoxTile, amount, fromPosition, toPosition);
        }

        // --- Core move ---
        return ExecuteMove(player, item, from, destination as IDynamicTile, amount, fromPosition, toPosition);
    }

    /// <summary>
    ///     Moves an item from one map tile to another, performing all validations.
    /// </summary>
    public Result<OperationResultList<IItem>> Move(IPlayer player, IItem item, IDynamicTile from,
        ITile destination, byte amount, byte fromPosition, byte? toPosition)
    {
        if (player is null || item is null || !item.CanBeMoved)
            return Result<OperationResultList<IItem>>.NotPossible;

        // --- Floor-level check (player must be on the same floor as the source item) ---
        if (item.Location.Type == LocationType.Ground)
        {
            if (item.Location.Z < player.Location.Z)
            {
                OperationFailService.Send(player.CreatureId, TextConstants.FIRST_GO_UPSTAIRS);
                return Result<OperationResultList<IItem>>.NotPossible;
            }

            if (item.Location.Z > player.Location.Z)
            {
                OperationFailService.Send(player.CreatureId, TextConstants.FIRST_GO_DOWNSTAIRS);
                return Result<OperationResultList<IItem>>.NotPossible;
            }
        }
        
        // --- Walk-to if the source item is not close to the player ---
        if (!item.IsCloseTo(player))
        {
            walkToMechanism.WalkTo(player,
                () => Move(player, item, from, destination, amount, fromPosition, toPosition),
                item.Location);
            return Result<OperationResultList<IItem>>.Success;
        }

        // --- Throw validation (distance + line-of-sight + special tile exemptions) ---
        // Validation must happen against the ORIGINAL destination so that special tiles
        // (teleport, hole, floor-change) are recognized before they get resolved away.
        var throwValidation = ValidateThrow(player, item, destination);
        if (throwValidation.Failed)
        {
            SendThrowError(player, throwValidation.Reason);
            return new Result<OperationResultList<IItem>>(throwValidation.Reason);
        }

        // --- Resolve destination tile for map targets ---
        destination = ResolveDestination(destination);


        // --- Liquid source (water) / trash holder handling ---
        // Items thrown onto water or trash-holder tiles are consumed: removed from source but not placed on the tile.
        if (destination.HasFlag(TileFlags.TrashHolder))
        {
            EventAggregator.Invoke(new ItemMovedToTrashHolder(item, destination));
            return ConsumeItem(item, from, amount, fromPosition);
        }

        // --- Mail box handling ---
        if (destination.HasFlag(TileFlags.MailBox) && destination is IDynamicTile mailBoxTile)
        {
            if (!item.IsMailable)
            {
                OperationFailService.Send(player, InvalidOperation.NotPossible);
                return Result<OperationResultList<IItem>>.NotPossible;
            }

            return HandleMailBoxMove(player, item, from, mailBoxTile, amount, fromPosition, toPosition);
        }
        
        // --- Core move ---
        return ExecuteMove(player, item, from, destination as IDynamicTile, amount, fromPosition, toPosition);
    }

    #region Validation

    /// <summary>
    ///     Validates the throw using <see cref="IItemThrowValidator" /> (distance, line-of-sight,
    ///     special tile exemptions).
    /// </summary>
    private Result ValidateThrow(IPlayer player, IItem item, ITile destination)
    {
        return itemThrowValidator.Validate(player, item.Location, destination.Location, destination);
    }

    /// <summary>
    ///     Sends the appropriate error message to the player based on the validation failure reason.
    /// </summary>
    private static void SendThrowError(IPlayer player, InvalidOperation reason)
    {
        switch (reason)
        {
            case InvalidOperation.TooFar:
                OperationFailService.Send(player.CreatureId, TextConstants.DESTINATION_IS_OUT_OF_REACH);
                break;
            case InvalidOperation.NotEnoughRoom:
                OperationFailService.Send(player.CreatureId, TextConstants.NOT_ENOUGH_ROOM);
                break;
            default:
                OperationFailService.Send(player.CreatureId, TextConstants.YOU_CANNOT_THROW_THERE);
                break;
        }
    }

    #endregion

    #region Destination Resolution

    /// <summary>
    ///     Resolves the final destination tile by following teleports, holes, and floor-change
    ///     tiles via <see cref="IMap.GetFinalDestination" />.
    /// </summary>
    private ITile ResolveDestination(ITile destination) => map.GetFinalDestination(destination.Location);

    #endregion

    #region Core Move Mechanics

    /// <summary>
    ///     Consumes an item by removing it from the source without placing it anywhere.
    ///     Used for water / trash-holder tiles.
    /// </summary>
    private static Result<OperationResultList<IItem>> ConsumeItem(IItem item, IHasItem from, byte amount,
        byte fromPosition)
    {
        return from.RemoveItem(item, amount, fromPosition, out _);
    }

    /// <summary>
    ///     Handles moving an item to a mailbox tile. If the mail send succeeds, the item
    ///     is consumed; otherwise, it falls back to a normal move to the tile.
    /// </summary>
    private Result<OperationResultList<IItem>> HandleMailBoxMove(IPlayer player, IItem item, IHasItem from,
        IHasItem destination, byte amount, byte fromPosition, byte? toPosition)
    {
        var movement = new ItemMovementContext(player, item, from, destination, amount, fromPosition, toPosition);
        var canAdd = destination.CanAddItem(movement);
        if (canAdd.Failed) return new Result<OperationResultList<IItem>>(canAdd.Reason);

        var possibleAmountToAdd = destination.PossibleAmountToAdd(movement);
        if (possibleAmountToAdd == 0)
            return new Result<OperationResultList<IItem>>(InvalidOperation.NotEnoughRoom);

        var amountToRemove = item is ICumulative
            ? (byte)Math.Min(amount, possibleAmountToAdd)
            : (byte)1;
        var removeResult = from.RemoveItem(item, amountToRemove, fromPosition, out var removedItem);
        if (removeResult.Failed || removedItem is null)
        {
            var reason = removeResult.Failed ? removeResult.Error : InvalidOperation.NotPossible;
            return new Result<OperationResultList<IItem>>(reason);
        }

        var sendMailResult = mailService.Send(player, removedItem);
        if (sendMailResult.Succeeded)
        {
            removedItem.SetNewLocation(Location.Zero);
            if (removedItem is IMovableThing movableThing && destination is IThing destinationThing)
            {
                movableThing.OnMoved(destinationThing);
            }

            var remainingAmount = (byte)(amount - amountToRemove);
            return remainingAmount > 0
                ? HandleMailBoxMove(player, item, from, destination, remainingAmount, fromPosition, toPosition)
                : Result<OperationResultList<IItem>>.Success;
        }

        var addition = movement with { Item = removedItem, Amount = removedItem.Amount };
        var addResult = destination.AddItem(removedItem, addition);
        if (addResult.Failed)
        {
            var restoreResult = RestoreRemovedItem(player, removedItem, from, destination, fromPosition, toPosition);
            if (restoreResult.Failed) return restoreResult;
            return addResult;
        }

        if (removedItem is IMovableThing fallbackMovableItem && destination is IThing fallbackDestination)
        {
            fallbackMovableItem.OnMoved(fallbackDestination);
        }

        var fallbackRemainingAmount = (byte)(amount - amountToRemove);
        return fallbackRemainingAmount > 0
            ? HandleMailBoxMove(player, item, from, destination, fallbackRemainingAmount, fromPosition, toPosition)
            : addResult;
    }

    /// <summary>
    ///     Executes the core item move: validates capacity, removes from source, adds to destination.
    ///     The shared transfer operation handles cumulative overflow iteratively.
    /// </summary>
    private Result<OperationResultList<IItem>> ExecuteMove(IPlayer player, IItem item, IHasItem from,
        IHasItem destination, byte amount, byte fromPosition, byte? toPosition)
    {
        var movement = new ItemMovementContext(player, item, from, destination, amount, fromPosition, toPosition);
        return ItemTransferOperation.Execute(movement);
    }

    private static Result<OperationResultList<IItem>> RestoreRemovedItem(IPlayer player, IItem item,
        IHasItem source, IHasItem failedDestination, byte sourcePosition, byte? failedDestinationPosition)
    {
        var destinationPosition = source is IInventory ? sourcePosition : (byte?)null;
        var restoreMovement = new ItemMovementContext(
            player,
            item,
            failedDestination,
            source,
            item.Amount,
            failedDestinationPosition ?? 0,
            destinationPosition);
        var canRestore = source.CanAddItem(restoreMovement);
        if (canRestore.Failed)
            return new Result<OperationResultList<IItem>>(canRestore.Reason);

        return source.AddItem(item, restoreMovement);
    }

    #endregion
}
