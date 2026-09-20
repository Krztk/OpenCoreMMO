using NeoServer.Domain.Common.Contracts;
using NeoServer.Domain.Common.Contracts.Creatures;
using NeoServer.Domain.Common.Contracts.Items;

namespace NeoServer.Domain.Common.Creatures.Structs;

public readonly record struct ItemMovementContext(
    IPlayer Actor,
    IItem Item,
    IHasItem Source,
    IHasItem Destination,
    byte Amount,
    byte SourcePosition,
    byte? DestinationPosition);
