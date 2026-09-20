using NeoServer.Domain.Common.Contracts.Items.Types;

namespace NeoServer.Domain.Items.Items.Containers.Container.Queries;

internal static class FindSlotOfFirstItemNotFullyQuery
{
    public static int Find(IContainer onContainer, ICumulative cumulativeItem, byte? preferredPosition = null,
        bool autoStack = true)
    {
        if (preferredPosition is { } position && position < onContainer.SlotsUsed)
        {
            var preferredItem = onContainer.Items[position];
            if (!ReferenceEquals(preferredItem, cumulativeItem) && IsCompatible(preferredItem, cumulativeItem))
                return position;
        }

        if (!autoStack) return -1;

        for (var slotIndex = 0; slotIndex < onContainer.SlotsUsed; slotIndex++)
        {
            var itemOnSlot = onContainer.Items[slotIndex];
            if (ReferenceEquals(itemOnSlot, cumulativeItem)) continue;
            if (IsCompatible(itemOnSlot, cumulativeItem)) return slotIndex;
        }

        return -1;
    }

    private static bool IsCompatible(NeoServer.Domain.Common.Contracts.Items.IItem item,
        ICumulative cumulativeItem)
    {
        return item.ClientId == cumulativeItem.ClientId && item is ICumulative { Amount: < 100 };
    }
}
