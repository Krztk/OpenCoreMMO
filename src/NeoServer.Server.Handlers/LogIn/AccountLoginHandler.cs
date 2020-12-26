﻿using NeoServer.Data.Interfaces;
using NeoServer.Networking.Packets.Incoming;
using NeoServer.Networking.Packets.Outgoing;
using NeoServer.Server.Contracts.Network;

namespace NeoServer.Server.Handlers.Authentication
{
    public class AccountLoginHandler : PacketHandler
    {
        //private readonly IAccountRepository _repository;
        private readonly IAccountRepositoryNeo _repositoryNeo;
        public AccountLoginHandler(/*IAccountRepository repository,*/ IAccountRepositoryNeo repositoryNeo)
        {
            //_repository = repository;
            _repositoryNeo = repositoryNeo;
        }

        public override void HandlerMessage(IReadOnlyNetworkMessage message, IConnection connection)
        {
            var account = new AccountLoginPacket(message);

            connection.SetXtea(account.Xtea);

            if (account == null)
            { //todo: use option
                connection.Disconnect("Invalid account.");
                return;
            }

            if (!account.IsValid())
            {
                connection.Disconnect("Invalid account name or password."); //todo: use gameserverdisconnect
                return;
            }

            //var foundedAccount = _repository.Get(account.Account, account.Password);
            var foundedAccount = _repositoryNeo.Login(account.Account, account.Password).Result;

            if (foundedAccount == null)
            {
                connection.Disconnect("Account name or password is not correct.");
                return;
            }

            connection.Send(new CharacterListPacket(foundedAccount));
        }
    }
}
