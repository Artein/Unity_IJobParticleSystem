using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.ParticleSystemJobs;

namespace Game
{
    [RequireComponent(typeof(ParticleSystem))]
    public class RaycastingParticleSystem : MonoBehaviour
    {
        [SerializeField] private float _bounciness = 0.5f;
        
        private ParticleSystem _particleSystem;

        private void Awake()
        {
            _particleSystem = GetComponent<ParticleSystem>();
        }

        private void OnParticleUpdateJobScheduled()
        {
            var particlesCount = _particleSystem.particleCount;
            var commands = new NativeArray<RaycastCommand>(particlesCount, Allocator.TempJob);

            var buildRaycastCommandsJob = new BuildRaycastCommandsJob { DeltaTime = Time.deltaTime, Commands = commands };
            var jobHandle = buildRaycastCommandsJob.ScheduleBatch(_particleSystem, 512);
            
            var raycastHits = new NativeArray<RaycastHit>(particlesCount, Allocator.TempJob);
            jobHandle = RaycastCommand.ScheduleBatch(commands, raycastHits, 512, jobHandle);

            var applyHitsJob = new ApplyHitsJob { Bounciness = _bounciness, Hits = raycastHits };
            jobHandle = applyHitsJob.ScheduleBatch(_particleSystem, 512, jobHandle);

            commands.Dispose(jobHandle);
            raycastHits.Dispose(jobHandle);
        }
        
        [BurstCompile]
        private struct ApplyHitsJob : IJobParticleSystemParallelForBatch
        {
            [ReadOnly] public NativeArray<RaycastHit> Hits;
            [ReadOnly] public float Bounciness;
            
            public void Execute(ParticleSystemJobData jobData, int startIndex, int count)
            {
                var velocities = jobData.velocities;
                var endIndex = startIndex + count;
                for (int i = 0; i < endIndex; i += 1)
                {
                    var hit = Hits[i];
                    if (hit.distance > 0)
                    {
                        var velocity = velocities[i];
                        velocity = Vector3.Reflect(velocity, hit.normal) * Bounciness;
                        velocities[i] = velocity;
                    }
                }
            }
        }

        [BurstCompile]
        private struct BuildRaycastCommandsJob : IJobParticleSystemParallelForBatch
        {
            public NativeArray<RaycastCommand> Commands;
            [ReadOnly] public float DeltaTime;

            public void Execute(ParticleSystemJobData jobData, int startIndex, int count)
            {
                int endIndex = startIndex + count;
                for (int i = startIndex; i < endIndex; i += 1)
                {
                    var position = jobData.positions[i];
                    var velocity = jobData.velocities[i];
                    var speed = velocity.magnitude;
                    var direction = Vector3.Normalize(velocity);
                    var distance = speed * DeltaTime;
                    Commands[i] = new RaycastCommand(Physics.defaultPhysicsScene, position, direction, QueryParameters.Default, distance);
                }
            }
        }
    }
}