﻿using System;
using NeoServer.Game.Common.Contracts.Creatures;
using NeoServer.Game.Common.Contracts.Items.Types.Containers;
using NeoServer.Game.Common.Contracts.Items.Types.Usable;
using NeoServer.Game.Common.Contracts.Services;
using NeoServer.Game.Common.Location;
using NeoServer.Networking.Packets.Incoming;
using NeoServer.Server.Common.Contracts.Commands;
using NeoServer.Server.Common.Contracts.Scripts;

namespace NeoServer.Server.Commands.Player.UseItem;

public class PlayerUseItemCommand : ICommand
{
    private readonly ItemFinderService _itemFinderService;
    private readonly ILuaGameManager _luaGameManager;
    private readonly PlayerOpenDepotCommand _playerOpenDepotCommand;
    private readonly IPlayerUseService _playerUseService;

    public PlayerUseItemCommand(
        IPlayerUseService playerUseService,
        PlayerOpenDepotCommand playerOpenDepotCommand,
        ItemFinderService itemFinderService,
        ILuaGameManager luaGameManager)
    {
        _playerUseService = playerUseService;
        _playerOpenDepotCommand = playerOpenDepotCommand;
        _itemFinderService = itemFinderService;
        _luaGameManager = luaGameManager;
    }

    public void Execute(IPlayer player, UseItemPacket useItemPacket)
    {
        var item = _itemFinderService.Find(player, useItemPacket.Location, useItemPacket.ClientId);

        if (_luaGameManager.PlayerUseItem(player, useItemPacket.Location, useItemPacket.StackPosition,
                useItemPacket.Index, item))
            return;

        Action action;

        switch (item)
        {
            case null:
                return;
            case IDepot depot:
                action = () => _playerOpenDepotCommand.Execute(player, depot, useItemPacket);
                break;
            case IContainer container:
                action = () => _playerUseService.Use(player, container, useItemPacket.Index);
                break;
            case IUsableOn usableOn:
                action = () => _playerUseService.Use(player, usableOn, player);
                break;
            default:
                action = () => _playerUseService.Use(player, item);
                break;
        }

        if (useItemPacket.Location.Type == LocationType.Ground)
        {
            action();
            return;
        }

        action();
    }
}