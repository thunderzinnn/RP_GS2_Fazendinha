using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct InitialSeeds
{
    public CropData crop;
    [Min(0)] public int amount;
}

public class FarmInventory : MonoBehaviour
{
    [SerializeField] private InitialSeeds[] startingSeeds;

    private readonly Dictionary<CropData, int> seeds = new Dictionary<CropData, int>();
    private readonly Dictionary<CropData, int> produce = new Dictionary<CropData, int>();

    private void Awake()
    {
        foreach (InitialSeeds entry in startingSeeds)
            AddSeeds(entry.crop, entry.amount);
    }

    public int SeedCount(CropData crop) => Count(seeds, crop);
    public int ProduceCount(CropData crop) => Count(produce, crop);

    private int Count(Dictionary<CropData, int> items, CropData crop)
    {
        return crop != null && items.TryGetValue(crop, out int value) ? value : 0;
    }

    public void AddSeeds(CropData crop, int amount)
    {
        if (crop != null && amount > 0)
            seeds[crop] = SeedCount(crop) + amount;
    }

    public bool TryUseSeed(CropData crop)
    {
        if (SeedCount(crop) < 1) return false;
        seeds[crop]--;
        return true;
    }

    public void AddProduce(CropData crop, int amount)
    {
        if (crop != null && amount > 0)
            produce[crop] = ProduceCount(crop) + amount;
    }

    public bool TryRemoveProduce(CropData crop, int amount)
    {
        if (amount <= 0 || ProduceCount(crop) < amount) return false;
        produce[crop] -= amount;
        return true;
    }
}
