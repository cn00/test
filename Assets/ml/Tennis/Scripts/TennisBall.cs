using System;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace ml.Tennis
{
    public class TennisBall : MonoBehaviour
    {
        public TennisPlayground playground;
        public Rigidbody rb;

        [Tooltip("触网")]
        public bool net;

        public enum AgentRole
        {
            A,
            B,
            O,
        }

        public TennisAgentA lastHitAgent = null;

        public enum FloorHit
        {
            Service,
            FloorHitUnset,
            FloorAHit,
            FloorBHit
        }

        public FloorHit lastFloorHit;


        public Vector3 velocityMinInit = new Vector3(60f, 5f, 3f);
        public Vector3 velocityMax = new Vector3(60f, 10f, 10f); // 世界记录 196Km/h = 54m/s
        public Vector3 Velocity
            #if !UNITY_EDITOR
            => rb.velocity
            #endif
            ;
        
        private void FixedUpdate()
        {
            // if(playground.agentA is TennisAgent)// 
            // {
            //     var rgV = rigidbody.velocity;
            //     rigidbody.velocity = new Vector3(
            //         Mathf.Clamp(rgV.x, -9f, 9f),
            //         Mathf.Clamp(rgV.y, -9f, 9f),
            //         Mathf.Clamp(rgV.z, -9f, 9f));
            // }
            // else

            // var rgV = rigidbody.velocity;
            // rigidbody.velocity = new Vector3(
            //     Mathf.Clamp(rgV.x, -maxVelocity.x, maxVelocity.z),
            //     Mathf.Clamp(rgV.y, -maxVelocity.y, maxVelocity.y),
            //     Mathf.Clamp(rgV.z, -maxVelocity.z, maxVelocity.z));
            
            #if UNITY_EDITOR
            Velocity = rb.velocity;
            #endif

            // // 无法预计算轨迹
            // var bp = transform.localPosition;
            // transform.localPosition = new Vector3(
            //     Mathf.Clamp(bp.x, -4f * playground.Size.x, 4f * playground.Size.x),
            //     Mathf.Clamp(bp.y, 0, 10f * playground.Size.y),
            //     Mathf.Clamp(bp.z, -4f * playground.Size.z, 4f * playground.Size.z));

        }

        void Reset()
        {
            if (playground.agentA.score > playground.levelOne && playground.agentB.score > playground.levelOne)
            {
            }

            var px = 8f;// Random.Range(-playground.HalfSize.x, playground.HalfSize.x) > 0 ? 8f : -8f;
            var py = 2f;//Random.Range(4f, playground.HalfSize.y);//
            var pz = 0f;// Random.Range(-playground.HalfSize.z, playground.HalfSize.z);
            transform.localPosition = new Vector3(px, py, pz);

            var vx = -12.6f;// (px > 0f ? -1f:1f) * Random.Range(velocityMinInit.x, velocityMax.x); // * 14f; // 
            var vy = 3.5f;// Random.Range(velocityMinInit.y, velocityMax.y); // 3.5f;//
            var vz = -3f;// Random.Range(velocityMinInit.z, velocityMax.z);
            rb.velocity = new Vector3(vx, vy,vz);
            
            playground.agentA.EndEpisode();
            playground.agentB.EndEpisode();
            playground.agentA2.EndEpisode();
            playground.agentB2.EndEpisode();
            lastFloorHit = FloorHit.Service;
            lastHitAgent = null;
            net = false;
        }

        void AgentAWins(float reward = 1)
        {
            playground.agentA. SetReward( reward/2f);
            playground.agentB. SetReward(-reward/2f);
            playground.agentA2.SetReward( reward/2f);
            playground.agentB2.SetReward(-reward/2f);
        }

        void AgentBWins(float reward = 1)
        {
            playground.agentA. SetReward(-reward/2f);
            playground.agentB. SetReward( reward/2f);
            playground.agentA2.SetReward(-reward/2f);
            playground.agentB2.SetReward( reward/2f);
        }

        public Action<Collision, string> CollisionEnter;

        /*
        void OnTriggerEnter(Collision collision)
        {
            if (CollisionEnter!=null)
            {
                CollisionEnter(collision);
            }
            if (collision.gameObject.name == "over")
            {
                // agent can return serve in the air
                if (lastFloorHit != FloorHit.FloorHitUnset && !net)
                {
                    net = true;
                }
        
                if (lastAgentHit == AgentRole.A)
                {
                    playground.agentA.AddReward(0.6f);
                }
                else if (lastAgentHit == AgentRole.B)
                {
                    playground.agentB.AddReward(0.6f);
                }
            }
        }
        */

        private Collision currentCollision;


        void OnCollisionEnter(Collision collision)
        {
            currentCollision = collision;
            if (CollisionEnter != null)
            {
                CollisionEnter(collision, "Enter");
            }

            if (collision.gameObject.CompareTag("iWall"))
            {
                if (collision.gameObject.name == "wallA")
                {
                    // Agent A hits into wall or agent B hit a winner
                    if ((lastHitAgent && !lastHitAgent.invertX) 
                        || lastFloorHit == FloorHit.FloorAHit)
                    {
                        if (lastHitAgent && lastHitAgent.invertX) // agent B hit a winner
                        {
                            lastHitAgent.Wins();
                        }
                        else                      // Agent A hits into wall
                            AgentBWins();
                    }
                    // Agent B hits long
                    else // if (lastAgentHit == AgentRole.B)
                    {
                        AgentAWins();
                    }
                    Reset();
                }
                else if (collision.gameObject.name == "wallB")
                {
                    // Agent B hits into wall or agent A hit a winner
                    if ((lastHitAgent && lastHitAgent.invertX) 
                        || lastFloorHit == FloorHit.FloorBHit)
                    {
                        if (lastHitAgent && !lastHitAgent.invertX) // agent A hit a winner
                        {
                            lastHitAgent.Wins();
                        }
                        else                      // Agent B hits into wall
                            AgentAWins();
                    }
                    // Agent A hits long
                    else // if (lastAgentHit == AgentRole.A)
                    {
                        AgentBWins();
                    }
                    Reset();
                }
                else if (collision.gameObject.name == "floorA")
                {
                    // Agent A hits into floor, double bounce or service
                    if (   lastFloorHit == FloorHit.FloorAHit // double bounce
                        // || lastFloorHit == FloorHit.Service
                        )
                    {
                        if (lastHitAgent && lastHitAgent.invertX) // agent B hit a winner
                        {
                            lastHitAgent.Wins();
                        }
                        else                      // Agent A hits into floor
                            AgentBWins();
                        Reset();
                    }
                    else
                    {
                        lastFloorHit = FloorHit.FloorAHit;
                    }
                }
                else if (collision.gameObject.name == "floorB")
                {
                    // Agent B hits into floor, double bounce or service
                    if (lastFloorHit == FloorHit.FloorBHit
                        // || lastFloorHit == FloorHit.Service
                        )
                    {
                        if (lastHitAgent && !lastHitAgent.invertX) // agent A hit a winner
                        {
                            lastHitAgent.Wins();
                        }
                        else                      // Agent B hits into floor
                            AgentAWins();
                        Reset();
                    }
                    else
                    {
                        lastFloorHit = FloorHit.FloorBHit;
                    }
                }
                else if (collision.gameObject.name == "net")
                {
                    if (lastHitAgent && !lastHitAgent.invertX)
                    {
                        AgentBWins();
                    }
                    else // if (lastAgentHit == AgentRole.B)
                    {
                        AgentAWins();
                    }
                }
            }
            else if (collision.gameObject.CompareTag("agent"))
            {
                var agent = collision.gameObject.GetComponent<TennisAgentA>();
                agent.AddReward(0.6f);
                ++agent.hitCount;
                if (!agent.invertX) // A
                {
                    // Agent A double hit
                    if (lastHitAgent && !lastHitAgent.invertX)
                    {
                        AgentBWins();
                    }
                    else
                    {
                        lastFloorHit = FloorHit.FloorHitUnset;
                    }
                }
                else // if (collision.gameObject.name == "AgentB")
                {
                    // Agent B double hit
                    if (lastHitAgent && lastHitAgent.invertX)
                    {
                        AgentAWins();
                    }
                    else
                    {
                        lastFloorHit = FloorHit.FloorHitUnset;
                    }
                }
                lastHitAgent = agent;
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            currentCollision = collision;
            if (CollisionEnter != null)
            {
                CollisionEnter(collision, "Exit");
            }
        }
    }
}