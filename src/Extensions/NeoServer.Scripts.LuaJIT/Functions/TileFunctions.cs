﻿using LuaNET;
using NeoServer.Game.Common.Contracts.Items;
using NeoServer.Game.Common.Contracts.World.Tiles;
using NeoServer.Game.Common.Item;
using NeoServer.Game.Common.Location;
using NeoServer.Game.Common.Location.Structs;
using NeoServer.Server.Common.Contracts;
using Serilog;

namespace NeoServer.Scripts.LuaJIT.Functions;

public class TileFunctions : LuaScriptInterface, ITileFunctions
{
    private static IGameServer _gameServer;

    public TileFunctions(
        ILuaEnvironment luaEnvironment, IGameServer gameServer, ILogger logger) : base(nameof(TileFunctions))
    {
        _gameServer = gameServer;
    }

    public void Init(LuaState L)
    {
        RegisterSharedClass(L, "Tile", "", LuaCreateTile);
        RegisterMetaMethod(L, "Tile", "__eq", LuaUserdataCompare<ITile>);
        RegisterMethod(L, "Tile", "getPosition", LuaGetPosition);
        RegisterMethod(L, "Tile", "getGround", LuaGetGround);
        RegisterMethod(L, "Tile", "getItems", LuaTileGetItems);
        RegisterMethod(L, "Tile", "getItemCount", LuaTileGetItemCount);
        RegisterMethod(L, "Tile", "hasProperty", LuaTileHasProperty);
        RegisterMethod(L, "Tile", "hasFlag", LuaTileHasFlag);
    }

    public static int LuaCreateTile(LuaState L)
    {
        // Tile(x, y, z)
        // Tile(position)
        ITile tile = null;

        if (Lua.IsTable(L, 2))
        {
            var position = GetPosition(L, 2);
            tile = _gameServer.Map.GetTile(position);
        }
        else
        {
            var z = GetNumber<byte>(L, 4);
            var y = GetNumber<ushort>(L, 3);
            var x = GetNumber<ushort>(L, 2);

            var position = new Location(x, y, z);

            tile = _gameServer.Map.GetTile(position);
        }

        PushUserdata(L, tile);
        SetMetatable(L, -1, "Tile");
        return 1;
    }

    public static int LuaGetPosition(LuaState L)
    {
        // tile:getPosition()
        var tile = GetUserdata<ITile>(L, 1);
        if (tile != null)
        {
            PushPosition(L, tile.Location);
        }
        else
            Lua.PushNil(L);

        return 1;
    }

    public static int LuaGetGround(LuaState L)
    {
        // tile:getGround()
        var tile = GetUserdata<ITile>(L, 1);
        if (tile != null && tile.TopItemOnStack != null)
        {
            PushUserdata(L, tile.TopItemOnStack);
            SetItemMetatable(L, -1, tile.TopItemOnStack);
        }
        else
            Lua.PushNil(L);

        return 1;
    }

    public static int LuaTileGetItems(LuaState L)
    {
        // tile:getItems()
        var tile = GetUserdata<ITile>(L, 1);

        if (!tile.HasThings || tile is not IDynamicTile dynamicTile)
        {
            Lua.PushNil(L);
            return 1;
        }

        Lua.CreateTable(L, tile.ThingsCount, 0);

        int index = 0;
        foreach (var item in dynamicTile.AllItems)
        {
            PushUserdata(L, item);
            SetItemMetatable(L, -1, item);
            Lua.RawSetI(L, -2, ++index);
        }

        return 1;
    }

    public static int LuaTileGetItemCount(LuaState L)
    {
        // tile:getItemCount()
        var tile = GetUserdata<ITile>(L, 1);
        if (tile != null)
        {
            Lua.PushNumber(L, tile.ThingsCount);
        }
        else
            Lua.PushNil(L);

        return 1;
    }

    public static int LuaTileHasProperty(LuaState L)
    {
        // tile:hasProperty(property[, item])
        var tile = GetUserdata<ITile>(L, 1);

        if(tile == null)
        {
            Lua.PushNil(L);
            return 1;
        }

        IItem? item = null;
        if (Lua.GetTop(L) >= 3)
            item = GetUserdata<IItem>(L, 3);

        var property = GetNumber<ItemFlag>(L, 2);

        if (item != null)
            Lua.PushBoolean(L, item.Metadata.HasFlag(property));
        else if (tile is IDynamicTile dynamicTile)
            Lua.PushBoolean(L, dynamicTile.AllItems.Any(c => c.Metadata.HasFlag(property)));
        else if (tile is IStaticTile staticTile)
            Lua.PushBoolean(L, staticTile.TopItemOnStack.Metadata.HasFlag(property));

        return 1;
    }

    public static int LuaTileHasFlag(LuaState L)
    {
        // tile:hasFlag(flag)
        var tile = GetUserdata<ITile>(L, 1);
        if (tile != null)
        {
            var flag = GetNumber<TileFlags>(L, 2);
            Lua.PushBoolean(L, tile.HasFlag(flag));
        }
        else
            Lua.PushNil(L);

        return 1;
    }
}
