using UnityEngine;

[CreateAssetMenu(fileName = "NewCrop", menuName = "Golden Root Quick Farmer/Crops")]
public class CropData : ScriptableObject
{
    [SerializeField] private string cropName = "Trigo";
    [Min(1)] public int seedPrice = 3;
    [Min(1)] public int sellPrice = 5;
    [Min(1)] public int harvestAmount = 3;
    [Min(0.1f)] public float plantedSeconds = 4f;
    [Min(0.1f)] public float growingSeconds = 8f;
    [Range(0f, 100f)] public float initialHydration = 35f;
    [Min(0f)] public float hydrationLossPerSecond = 3f;
    [Min(0.1f)] public float waterCost = 10f;
    [Min(0.1f)] public float hydrationPerWatering = 45f;

    [Header("Modelos opcionais por cultura (Prefabs)")]
    public GameObject plantedPrefab;
    public GameObject growingPrefab;
    public GameObject grownPrefab;

    public string CropName => cropName;
}
