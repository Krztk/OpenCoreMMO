﻿using NeoServer.Game.Common.Contracts.Items;
using NeoServer.Game.Common.Contracts.Items.Types.Containers;

namespace NeoServer.Game.Items.Items.Containers.Container;

public class ContainerWeight
{
    public ContainerWeight(IContainer container)
    {
        Weight = container.Metadata.Weight;
        SubscribeToEvents(container);
    }

    public float Weight { get; private set; }
    private void ChangeWeight(float weight) => Weight = weight;

    internal void UpdateWeight(IContainer onContainer, float weightChange)
    {
        ChangeWeight(Weight + weightChange);
        UpdateParent(onContainer, weightChange);
    }

    private void UpdateWeight(IContainer onContainer, byte slot, IItem tem, sbyte amount)
    {
        ChangeWeight(Weight + amount);
        UpdateParent(onContainer, amount);
    }

    private void IncreaseWeight(IItem item, IContainer container)
    {
        var weight = item is IMovableItem movableItem ? movableItem.Weight : 0;

        ChangeWeight(Weight + weight);
        UpdateParent(container, weight);
    }

    private void UpdateParent(IContainer initialContainer, float weight)
    {
        var parent = initialContainer.Parent;
        if (parent is not Container container) return;

        container.OnChildWeightUpdated(weight);
    }

    private void DecreaseWeight(IContainer fromContainer, byte slot, IItem item)
    {
        var weight = item is IMovableItem movableItem ? movableItem.Weight : 0;

        ChangeWeight(Weight - weight);
        UpdateParent(fromContainer, -weight);
    }

    private void SubscribeToEvents(IContainer container)
    {
        container.OnItemAdded += IncreaseWeight;
        container.OnItemRemoved += DecreaseWeight;
        container.OnItemUpdated += UpdateWeight;
    }
}