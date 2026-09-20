using NeoServer.Domain.Common.Contracts.Items;
using NeoServer.Domain.Common.Contracts.Items.Types;
using NeoServer.Domain.Items.Items.Containers.Container.Queries;

namespace NeoServer.Domain.Items.Items.Containers.Container.Calculations;

internal static class PossibleAmountToAddCalculation
{
    public static uint Calculate(Container container, IItem item, byte? position = null, bool autoStack = true)
    {
        if (item is not ICumulative) return container.IsFull ? 0 : container.FreeSlotsCount;

        var possibleAmountToAdd = container.FreeSlotsCount * 100;

        var itemToJoinSlot = FindSlotOfFirstItemNotFullyQuery.Find(container, (ICumulative)item, position, autoStack);
        if (itemToJoinSlot >= 0 && container.Items[itemToJoinSlot] is ICumulative itemToJoin)
            possibleAmountToAdd += itemToJoin.AmountToComplete;

        return possibleAmountToAdd;
    }
}
