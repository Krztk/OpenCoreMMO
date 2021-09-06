﻿using NeoServer.Game.DataStore;
using NeoServer.Networking.Packets.Incoming.Shop;
using NeoServer.Server.Common.Contracts;
using NeoServer.Server.Common.Contracts.Network;
using NeoServer.Server.Tasks;

namespace NeoServer.Networking.Handlers.Shop
{
    public class PlayerSaleHandler : PacketHandler
    {
        private readonly IGameServer game;
        private readonly ItemTypeStore _itemTypeStore;

        public PlayerSaleHandler(IGameServer game, ItemTypeStore itemTypeStore)
        {
            this.game = game;
            _itemTypeStore = itemTypeStore;
        }

        public override void HandlerMessage(IReadOnlyNetworkMessage message, IConnection connection)
        {
            var playerSalePacket = new PlayerSalePacket(message);
            if (!game.CreatureManager.TryGetPlayer(connection.CreatureId, out var player)) return;

            var serverId = ItemIdMapStore.Data.Get(playerSalePacket.ItemClientId);

            if (!_itemTypeStore.TryGetValue(serverId, out var itemType)) return;

            game.Dispatcher.AddEvent(new Event(() =>
                player.Sell(itemType, playerSalePacket.Amount, playerSalePacket.IgnoreEquipped)));
        }
    }
}