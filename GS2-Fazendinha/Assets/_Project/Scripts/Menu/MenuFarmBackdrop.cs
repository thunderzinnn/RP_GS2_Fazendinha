using System;
using Unity.Cinemachine;
using UnityEngine;

public class MenuFarmBackdrop : MonoBehaviour
{
    [Serializable]
    public class PlotVisual
    {
        public Transform workPoint;
        public Transform cropAnchor;
        public GameObject plantedModel;
        public GameObject growingModel;
        public GameObject grownModel;

        [NonSerialized] public float growthTimer;
        [NonSerialized] public bool growing;
        [NonSerialized] public bool grown;
    }

    private enum WorkPhase { Walking, Planting, Watering, Harvesting, Fertilizing }

    [Header("Decorative Farm")]
    [SerializeField] private PlotVisual[] plots = new PlotVisual[0];
    [SerializeField] private Transform farmer;
    [SerializeField] private Transform farmerVisual;
    [SerializeField] private Transform waterEffect;
    [SerializeField] private Transform harvestEffect;
    [SerializeField] private Transform fertilizerEffect;

    [Header("Sequence Timing")]
    [SerializeField, Min(0.1f)] private float walkingSpeed = 5f;
    [SerializeField, Min(0.1f)] private float plantingSeconds = 0.7f;
    [SerializeField, Min(0.1f)] private float wateringSeconds = 1f;
    [SerializeField, Min(0.1f)] private float growingSeconds = 3.5f;
    [SerializeField, Min(0.1f)] private float harvestingSeconds = 0.8f;
    [SerializeField, Min(0.1f)] private float fertilizingSeconds = 0.7f;

    [Header("Cinemachine Orbit")]
    [SerializeField] private CinemachineOrbitalFollow cameraOrbit;
    [SerializeField] private float orbitDegreesPerSecond = 4.5f;

    private int plotIndex;
    private float actionTimer;
    private WorkPhase phase;

    private void Start()
    {
        InitializeSequence();
    }

    private void Update()
    {
        TickSequence(Time.unscaledDeltaTime);
        UpdateFarmerVisual();
        UpdateActionEffects();
        UpdateCameraOrbit();
    }

    private void InitializeSequence()
    {
        plotIndex = 0;
        actionTimer = 0f;
        phase = WorkPhase.Walking;
        foreach (var plot in plots)
        {
            plot.growing = plot.grown = false;
            ShowStage(plot, null);
        }
        if (waterEffect != null) waterEffect.gameObject.SetActive(false);
        if (harvestEffect != null) harvestEffect.gameObject.SetActive(false);
        if (fertilizerEffect != null) fertilizerEffect.gameObject.SetActive(false);
    }

    public void TickSequence(float seconds)
    {
        if (seconds <= 0f || plots.Length == 0 || farmer == null) return;
        UpdateCropGrowth(seconds);
        var plot = plots[plotIndex];
        if (phase == WorkPhase.Walking)
        {
            farmer.position = Vector3.MoveTowards(farmer.position, plot.workPoint.position, walkingSpeed * seconds);
            FacePoint(plot.workPoint.position, seconds);
            if (Vector3.Distance(farmer.position, plot.workPoint.position) < 0.02f)
                BeginPhase(plot.grown ? WorkPhase.Harvesting : WorkPhase.Planting);
            return;
        }

        FacePoint(plot.cropAnchor.position, seconds);
        actionTimer += seconds;
        if (phase == WorkPhase.Planting && actionTimer >= plantingSeconds)
        {
            ShowStage(plot, plot.plantedModel);
            BeginPhase(WorkPhase.Watering);
        }
        else if (phase == WorkPhase.Watering && actionTimer >= wateringSeconds)
        {
            BeginPhase(WorkPhase.Fertilizing);
        }
        else if (phase == WorkPhase.Fertilizing && actionTimer >= fertilizingSeconds)
        {
            ShowStage(plot, plot.growingModel);
            plot.growthTimer = 0f;
            plot.growing = true;
            plotIndex = (plotIndex + 1) % plots.Length;
            BeginPhase(WorkPhase.Walking);
        }
        else if (phase == WorkPhase.Harvesting && actionTimer >= harvestingSeconds)
        {
            plot.grown = false;
            ShowStage(plot, null);
            BeginPhase(WorkPhase.Planting);
        }
    }

    private void UpdateCropGrowth(float seconds)
    {
        foreach (var plot in plots)
        {
            if (!plot.growing) continue;
            plot.growthTimer += seconds;
            if (plot.growthTimer < growingSeconds) continue;
            plot.growing = false;
            plot.grown = true;
            ShowStage(plot, plot.grownModel);
        }
    }

    private void BeginPhase(WorkPhase next)
    {
        phase = next;
        actionTimer = 0f;
    }

    private void FacePoint(Vector3 point, float seconds)
    {
        Vector3 direction = point - farmer.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            farmer.rotation = Quaternion.Slerp(farmer.rotation, Quaternion.LookRotation(direction), seconds * 10f);
    }

    private void UpdateFarmerVisual()
    {
        if (farmerVisual == null) return;
        bool walking = phase == WorkPhase.Walking;
        float bend = walking ? 0f : Mathf.Abs(Mathf.Sin(actionTimer * 6f)) * 24f;
        farmerVisual.localRotation = Quaternion.Euler(bend, 0f, 0f);
        farmerVisual.localPosition = Vector3.up * (walking ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 11f)) * 0.04f : 0f);
    }

    private void UpdateActionEffects()
    {
        if (plots.Length == 0) return;
        AnimateEffect(waterEffect, phase == WorkPhase.Watering, false);
        AnimateEffect(harvestEffect, phase == WorkPhase.Harvesting, true);
        AnimateEffect(fertilizerEffect, phase == WorkPhase.Fertilizing, true);
    }

    private void AnimateEffect(Transform effect, bool visible, bool rising)
    {
        if (effect == null) return;
        var particles = effect.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            effect.position = plots[plotIndex].cropAnchor.position;
            if (effect.gameObject.activeSelf == visible) return;
            effect.gameObject.SetActive(visible);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (visible) particles.Play(true);
            return;
        }
        effect.gameObject.SetActive(visible);
        if (!visible) return;
        effect.position = plots[plotIndex].cropAnchor.position;
        for (int index = 0; index < effect.childCount; index++)
        {
            float progress = Mathf.Repeat(actionTimer * 1.8f + index * 0.22f, 1f);
            float angle = index * Mathf.PI * 0.65f;
            effect.GetChild(index).localPosition = new Vector3(Mathf.Cos(angle) * 0.45f,
                rising ? 0.5f + progress * 1.3f : 1.8f - progress * 1.5f, Mathf.Sin(angle) * 0.45f);
        }
    }

    private void UpdateCameraOrbit()
    {
        if (cameraOrbit != null)
            cameraOrbit.HorizontalAxis.Value = Mathf.Repeat(cameraOrbit.HorizontalAxis.Value +
                orbitDegreesPerSecond * Time.unscaledDeltaTime + 180f, 360f) - 180f;
    }

    private void ShowStage(PlotVisual plot, GameObject visible)
    {
        if (plot.plantedModel != null) plot.plantedModel.SetActive(plot.plantedModel == visible);
        if (plot.growingModel != null) plot.growingModel.SetActive(plot.growingModel == visible);
        if (plot.grownModel != null) plot.grownModel.SetActive(plot.grownModel == visible);
    }
}
