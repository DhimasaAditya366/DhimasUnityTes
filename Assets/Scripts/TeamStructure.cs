using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeamStructure : MonoBehaviour
{
    public enum StructureType
    {
        Gate,
        Fence
    }

    public enum TeamSide
    {
        Blue,   // Player side
        Red     // Enemy side
    }

    public StructureType type;
    public TeamSide side;

    private void Start()
    {
        gameObject.tag = type == StructureType.Gate ? "Gate" : "Fence";
    }


    public static Transform FindTargetStructure(TeamRole role, bool isPlayerTeam, StructureType structureType)
    {
        TeamSide targetSide;

        // Determine which team's structure we're looking for based on role and team
        if (role == TeamRole.Attacker)
        {
            // Attackers target the opposing team's structures
            targetSide = isPlayerTeam ? TeamSide.Red : TeamSide.Blue;
        }
        else // Defender
        {
            // Defenders target their own team's structures
            targetSide = isPlayerTeam ? TeamSide.Blue : TeamSide.Red;
        }

        // Find all structures of the specified type
        TeamStructure[] structures = GameObject.FindObjectsOfType<TeamStructure>();

        // Create a list to store matching structures
        List<Transform> matchingStructures = new List<Transform>();

        // Find all structures matching our criteria
        foreach (TeamStructure structure in structures)
        {
            if (structure.type == structureType && structure.side == targetSide)
            {
                matchingStructures.Add(structure.transform);
            }
        }

        // If we found any matching structures, return a random one
        if (matchingStructures.Count > 0)
        {
            int randomIndex = Random.Range(0, matchingStructures.Count);
            return matchingStructures[randomIndex];
        }

        Debug.LogWarning($"No {structureType} found for {targetSide} side!");
        return null;
    }
}
