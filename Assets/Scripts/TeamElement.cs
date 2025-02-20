using System.Collections;
using UnityEngine;
using System;

public class TeamElement : MonoBehaviour, ITeamRoleHandler
{
    public TeamRole currentRole;
    [SerializeField] private Renderer[] renderers;
    private Material[] originalMaterials;
    public bool isPlayerTeam;


    [Header("Activation Settings")]
    [SerializeField] private float playerActivationDelay = 3f;
    [SerializeField] private float enemyActivationDelay = 3f;
    private bool isActive = false;
    private Color currentTeamColor;

    // Properties
    public TeamRole CurrentRole => currentRole;
    private bool isTemporarilyInactive = false;
    private Coroutine inactiveCoroutine;
    // Events
    public delegate void ActivationHandler();
    public event ActivationHandler OnActivated;
    public event Action<TeamRole> OnRoleChangedWithRole;  // Changed to match Soldier's expectation

    private void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                originalMaterials[i] = new Material(renderers[i].material);
            }
        }
        SetInactive();
    }

    public void InitializeTeam(TeamRole initialRole)
    {
        currentRole = initialRole;
        isPlayerTeam = (initialRole == GameManager.Instance.PlayerRole);
        UpdateColor();
        StartActivationTimer();
    }

    private void StartActivationTimer()
    {
        isActive = false;
        float delay = IsPlayerTeam() ? playerActivationDelay : enemyActivationDelay;
        StartCoroutine(ActivationTimer(delay));
    }

    private bool IsPlayerTeam()
    {
        if (GameManager.Instance != null)
        {
            return currentRole == GameManager.Instance.PlayerRole;
        }
        return false;
    }

    private void SetActive()
    {
        isActive = true;
        UpdateColor();
        OnActivated?.Invoke();
    }

    private void SetInactive()
    {
        isActive = false;
        ApplyGreyscale();
    }

    private void ApplyGreyscale()
    {
        Color greyscale = new Color(0.5f, 0.5f, 0.5f, 1f);
        foreach (var renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.material.color = greyscale;
            }
        }
    }

    private IEnumerator ActivationTimer(float delay)
    {
        SetInactive();
        yield return new WaitForSeconds(delay);
        SetActive();
    }

    public void UpdateTeamRole(TeamRole newRole)
    {
        if (currentRole != newRole)
        {
            Debug.Log($"{gameObject.name} switching role from {currentRole} to {newRole}");
            currentRole = newRole;
            isPlayerTeam = (newRole == GameManager.Instance.PlayerRole);
            UpdateColor();
            OnRoleChangedWithRole?.Invoke(newRole);
        }
    }

    public void UpdateColor()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager instance not found!");
            return;
        }

        currentTeamColor = (currentRole == TeamRole.Attacker)
            ? GameManager.Instance.playerColor
            : GameManager.Instance.enemyColor;

        Color colorToApply = isActive ? currentTeamColor : new Color(0.5f, 0.5f, 0.5f, 1f);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].material = new Material(originalMaterials[i]);
                renderers[i].material.color = colorToApply;
            }
        }
    }

    public void OnTeamRoleChanged(TeamRole newRole)
    {
        UpdateTeamRole(newRole);
    }

    private void OnDestroy()
    {
        if (inactiveCoroutine != null)
        {
            StopCoroutine(inactiveCoroutine);
        }
        StopAllCoroutines();
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.material != null)
            {
                Destroy(renderer.material);
            }
        }
    }

    public void SetTemporaryInactive(float duration)
    {
        // Stop any existing inactive coroutine
        if (inactiveCoroutine != null)
        {
            StopCoroutine(inactiveCoroutine);
        }

        isTemporarilyInactive = true;
        ApplyGreyscale();
        inactiveCoroutine = StartCoroutine(TemporaryInactiveTimer(duration));
    }

    private IEnumerator TemporaryInactiveTimer(float duration)
    {
        yield return new WaitForSeconds(duration);
        isTemporarilyInactive = false;
        UpdateColor();
    }
}