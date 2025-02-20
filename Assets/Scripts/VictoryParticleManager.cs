using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VictoryParticleManager : MonoBehaviour
{
    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem playerVictoryParticles;
    [SerializeField] private ParticleSystem enemyVictoryParticles;

    private static VictoryParticleManager instance;
    public static VictoryParticleManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<VictoryParticleManager>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayVictoryParticles(bool isPlayerWin, Vector3 position)
    {
        ParticleSystem particlesToPlay = isPlayerWin ? playerVictoryParticles : enemyVictoryParticles;

        if (particlesToPlay != null)
        {
            // Create a temporary instance of the particle system
            ParticleSystem particles = Instantiate(particlesToPlay, position, Quaternion.identity);

            // Get the duration of the particle system
            float duration = particles.main.duration;

            // Destroy the particle system after it completes
            Destroy(particles.gameObject, duration);
        }
        else
        {
            Debug.LogWarning($"{(isPlayerWin ? "Player" : "Enemy")} victory particles not assigned!");
        }
    }
}
