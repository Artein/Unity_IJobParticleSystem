using Unity.Burst;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;

namespace Game
{
    [RequireComponent(typeof(ParticleSystem))]
    public class TargetSystem : MonoBehaviour
    {
        [SerializeField] private float _correctionStrength;
        [SerializeField] private Transform _target;
        private ParticleSystem _particleSystem;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        private void OnParticleUpdateJobScheduled()
        {
            if (_target != null)
            {
                var job = new TargetJob { Target = _target.position, CorrectionSpeed = _correctionStrength, DeltaTime = Time.deltaTime };
                job.Schedule(_particleSystem, 512);
            }
        }

        [BurstCompile]
        private struct TargetJob : IJobParticleSystemParallelFor
        {
            public Vector3 Target;
            public float CorrectionSpeed;
            public float DeltaTime;
            
            public void Execute(ParticleSystemJobData jobData, int index)
            {
                var positions = jobData.positions;
                var velocities = jobData.velocities;

                var position = positions[index];
                var direction = Target - position;

                var velocity = velocities[index];
                velocities[index] = Vector3.RotateTowards(velocity, direction, CorrectionSpeed * DeltaTime, 0f);
            }
        }
    }
}