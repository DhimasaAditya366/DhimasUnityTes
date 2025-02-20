#if UNITY_EDITOR // This ensures the code only compiles in the Unity Editor
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(TeamManager))]
public class TeamManagerEditor : Editor
{
    void OnSceneGUI()
    {
        TeamManager teamManager = (TeamManager)target;

        // Draw spawn area wireframe
        Color areaColor = teamManager.currentRole == TeamRole.Attacker ? Color.blue : Color.red;
        areaColor.a = 0.5f; // Make it semi-transparent
        Handles.color = areaColor;

        // Create the corners of the spawn area
        Vector3[] corners = new Vector3[4];
        corners[0] = new Vector3(teamManager.spawnAreaMin.x, 0, teamManager.spawnAreaMin.y);
        corners[1] = new Vector3(teamManager.spawnAreaMax.x, 0, teamManager.spawnAreaMin.y);
        corners[2] = new Vector3(teamManager.spawnAreaMax.x, 0, teamManager.spawnAreaMax.y);
        corners[3] = new Vector3(teamManager.spawnAreaMin.x, 0, teamManager.spawnAreaMax.y);

        // Draw solid area
        Handles.DrawSolidRectangleWithOutline(corners, areaColor, areaColor);

        // Optional: Add labels
        Handles.Label(Vector3.Lerp(corners[0], corners[2], 0.5f),
            teamManager.currentRole.ToString() + " Spawn Area");
    }
}
#endif // End of UNITY_EDITOR conditional compilation