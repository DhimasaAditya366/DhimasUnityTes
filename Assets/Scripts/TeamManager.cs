using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;

public class TeamManager : MonoBehaviour, ITeamRoleHandler
{
    [Header("Team Settings")]
    public TeamRole currentRole;
    public int soldiersCount = 3;
    public bool isPlayerTeam;
    [SerializeField] private float spawnEnergyCost = 2f; // Single energy cost per TeamManager

    [Header("Spawn Settings")]
    public GameObject soldierPrefab;
    public GameObject spawnZoneObject; // Reference to the spawn zone GameObject
    public LayerMask spawnAreaLayer;
    public float spawnHeight = 0f;

    [Header("Team Structures")]
    public GameObject[] teamStructures;

    [Header("Ball Settings")]
    public GameObject ballPrefab;
    public int ballsPerMatch = 3;  // Number of balls to spawn at match start
    public float spawnDelay = 0.5f; // Delay between ball spawns

    private List<Soldier> soldiers = new List<Soldier>();
    private List<TeamElement> teamElements = new List<TeamElement>();
    private Camera mainCamera;
    private static bool spawnForPlayer = true;
    public Vector2 spawnAreaMin;
    public Vector2 spawnAreaMax;
    private int currentBallCount;
    private bool hasSpawnedBallsThisMatch = false;

    public TeamRole CurrentRole => currentRole;

    private void OnEnable()
    {
        // Subscribe to match start event
        GameManager.OnMatchStart += HandleMatchStart;
    }

    private void OnDisable()
    {
        // Unsubscribe from match start event
        GameManager.OnMatchStart -= HandleMatchStart;
    }


    private void Start()
    {
        mainCamera = Camera.main;
        SetupSpawnArea();

        InitializeTeamStructures();
    }

    private void Update()
    {
        GameManager gameManager = GameManager.Instance;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            spawnForPlayer = !spawnForPlayer;
            Debug.Log($"Switched spawning to {(spawnForPlayer ? "Player" : "Enemy")} side");
        }

        // Only allow spawning if not paused and left mouse button is clicked
        if (Input.GetMouseButtonDown(0) && !gameManager.IsPaused)
        {
            // Make sure we're not clicking UI elements (like the pause button)
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                TrySpawnAtMousePosition();
            }
        }
    }

    private void HandleMatchStart()
    {
        if (currentRole == TeamRole.Attacker && !hasSpawnedBallsThisMatch)
        {
            hasSpawnedBallsThisMatch = true;
            ClearExistingBalls();
            StartCoroutine(SpawnMatchBalls());
        }
    }

    private void ClearExistingBalls()
    {
        Ball[] existingBalls = FindObjectsOfType<Ball>();
        foreach (Ball ball in existingBalls)
        {
            Destroy(ball.gameObject);
        }
        currentBallCount = 0;
    }

    private System.Collections.IEnumerator SpawnMatchBalls()
    {
        for (int i = 0; i < ballsPerMatch; i++)
        {
            Vector3 spawnPosition = GetRandomSpawnPosition();
            SpawnBall(spawnPosition);
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float randomX = Random.Range(spawnAreaMin.x, spawnAreaMax.x);
        float randomZ = Random.Range(spawnAreaMin.y, spawnAreaMax.y);
        return new Vector3(randomX, spawnHeight + 1f, randomZ);
    }

    private void SpawnBall(Vector3 position)
    {
        if (ballPrefab != null)
        {
            GameObject ballObj = Instantiate(ballPrefab, position, Quaternion.identity);
            Ball ball = ballObj.GetComponent<Ball>();

            if (ball != null)
            {
                currentBallCount++;
                ball.OnBallDestroyed += () =>  // Updated to use new event name
                {
                    currentBallCount--;
                };
            }

            Debug.Log($"Ball spawned at {position} by {(isPlayerTeam ? "Player" : "Enemy")} team");
        }
        else
        {
            Debug.LogError("Ball prefab not assigned in TeamManager!");
        }
    }

    private void SetupSpawnArea()
    {
        if (spawnZoneObject && spawnZoneObject.TryGetComponent<Collider>(out Collider col))
        {
            Bounds bounds = col.bounds;
            spawnAreaMin = new Vector2(bounds.min.x, bounds.min.z);
            spawnAreaMax = new Vector2(bounds.max.x, bounds.max.z);
        }
        else
        {
            Debug.LogError($"No spawn zone object or collider assigned for {(isPlayerTeam ? "Player" : "Enemy")} team!");
        }
    }

    private void TrySpawnAtMousePosition()
    {
        GameManager gameManager = GameManager.Instance;

        // Check if game is paused
        if (gameManager.IsPaused)
        {
            Debug.Log("Cannot spawn while game is paused");
            return;
        }

        // Use isPlayerTeam to determine which energy pool to use
        bool hasEnoughEnergy = isPlayerTeam ?
            gameManager.TryConsumePlayerEnergy(spawnEnergyCost) :
            gameManager.TryConsumeEnemyEnergy(spawnEnergyCost);

        if (!hasEnoughEnergy)
        {
            Debug.Log($"Not enough energy to spawn soldier for {(isPlayerTeam ? "player" : "enemy")}. Required: {spawnEnergyCost}");
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, spawnAreaLayer))
        {
            Vector3 spawnPosition = hit.point;
            spawnPosition.y = spawnHeight;

            if (IsWithinSpawnArea(spawnPosition))
            {
                SpawnSoldierAtPosition(spawnPosition);
                Debug.Log($"Spawned {(isPlayerTeam ? "player" : "enemy")} soldier. Energy cost: {spawnEnergyCost}");
            }
            else
            {
                Debug.Log($"Click position {spawnPosition} outside spawn area bounds: " +
                    $"X({spawnAreaMin.x} to {spawnAreaMax.x}), Z({spawnAreaMin.y} to {spawnAreaMax.y})");

                // Refund the energy using isPlayerTeam to determine which pool to refund
                if (isPlayerTeam)
                {
                    gameManager.currentPlayerEnergy += spawnEnergyCost;
                }
                else
                {
                    gameManager.currentEnemyEnergy += spawnEnergyCost;
                }
                gameManager.UpdateEnergyUI();
            }
        }
    }

    private bool IsWithinSpawnArea(Vector3 position)
    {
        bool withinX = position.x >= spawnAreaMin.x && position.x <= spawnAreaMax.x;
        bool withinZ = position.z >= spawnAreaMin.y && position.z <= spawnAreaMax.y;
        return withinX && withinZ;
    }

    private void SpawnSoldierAtPosition(Vector3 position)
    {
        GameObject soldierObj = Instantiate(soldierPrefab, position, Quaternion.identity);
        soldierObj.transform.SetParent(transform);

        Soldier soldier = soldierObj.GetComponent<Soldier>();
        TeamElement teamElement = soldierObj.GetComponent<TeamElement>();

        if (teamElement != null)
        {
            teamElement.InitializeTeam(currentRole);
            teamElements.Add(teamElement);
        }

        if (soldier != null)
        {
            soldiers.Add(soldier);
        }
    }

    private void InitializeTeamStructures()
    {
        if (teamStructures != null)
        {
            foreach (GameObject structure in teamStructures)
            {
                if (structure != null)
                {
                    TeamElement teamElement = structure.GetComponent<TeamElement>();
                    if (teamElement != null)
                    {
                        teamElement.InitializeTeam(currentRole);
                        teamElements.Add(teamElement);
                    }
                }
            }
        }
    }

    public void UpdateTeamRole(TeamRole newRole)
    {
        if (currentRole != newRole)
        {
            currentRole = newRole;
            hasSpawnedBallsThisMatch = false;  // Reset the flag when role changes
            UpdateAllTeamElements();
        }
    }

    private void UpdateAllTeamElements()
    {
        foreach (TeamElement element in teamElements)
        {
            if (element != null)
            {
                element.UpdateTeamRole(currentRole);
            }
        }
    }

    public void OnTeamRoleChanged(TeamRole newRole)
    {
        UpdateTeamRole(newRole);
    }

    public void DeactivateAllSoldiers()
    {
        foreach (Soldier soldier in soldiers)
        {
            if (soldier != null)
                soldier.Deactivate();
        }
    }

    public void ActivateAllSoldiers()
    {
        foreach (Soldier soldier in soldiers)
        {
            if (soldier != null)
                soldier.Activate();
        }
    }

    public void ClearAllSoldiers()
    {
        foreach (Soldier soldier in soldiers.ToArray()) // Use ToArray to avoid collection modification issues
        {
            if (soldier != null)
            {
                Destroy(soldier.gameObject);
            }
        }
        soldiers.Clear(); // Clear the list after destroying all soldiers

        // Also clear team elements that are soldiers
        teamElements.RemoveAll(element => element != null && element.GetComponent<Soldier>() != null);
    }

    public void SetTeamPauseState(bool pauseState)
    {
        foreach (Soldier soldier in soldiers)
        {
            if (soldier != null)
            {
                soldier.SetPauseState(pauseState);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!spawnZoneObject) return;

        // Set color based on team with transparency
        Gizmos.color = isPlayerTeam ?
            new Color(0, 1, 0, 0.3f) : // Green for player
            new Color(1, 0, 0, 0.3f);  // Red for enemy

        Vector3 center = new Vector3(
            (spawnAreaMin.x + spawnAreaMax.x) * 0.5f,
            spawnHeight,
            (spawnAreaMin.y + spawnAreaMax.y) * 0.5f
        );

        Vector3 size = new Vector3(
            Mathf.Abs(spawnAreaMax.x - spawnAreaMin.x),
            0.1f,
            Mathf.Abs(spawnAreaMax.y - spawnAreaMin.y)
        );

        // Draw both wire and solid cube
        Gizmos.DrawWireCube(center, size);
        Gizmos.DrawCube(center, size);

        // Draw corner markers
        Gizmos.color = Color.yellow;
        float pointSize = 0.2f;
        Gizmos.DrawSphere(new Vector3(spawnAreaMin.x, spawnHeight, spawnAreaMin.y), pointSize);
        Gizmos.DrawSphere(new Vector3(spawnAreaMax.x, spawnHeight, spawnAreaMax.y), pointSize);
    }
}