using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Sensors.Reflection;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace ml.Tennis
{
    public class TennisAgentA : Agent
    {
        #region Properties
        [Header("==TennisAgent==")]
        public TennisPlayground pg;
        public Rigidbody rb;

        public TennisAgentA[] m_Adversaries = null;
        public TennisAgentA m_Partner = null;

        public TennisAgentA Partner
        {
            get
            {
                if(m_Partner == null)
                {
                    foreach (var i in pg.agents)
                    {
                        if (i.InvertMult == InvertMult && i.Id != Id)
                            return (m_Partner = i);
                    }
                }
                return m_Partner;
            }
        }

        public TennisAgentA[] Adversaries
        {
            get
            {
                if (m_Adversaries == null)
                {
                    var l = new List<TennisAgentA>();
                    foreach (var i in pg.agents)
                    {
                        if (i.InvertMult != InvertMult)
                            l.Add(i);
                    }

                    m_Adversaries = l.ToArray();
                }
                return m_Adversaries;
            }
        }
        public uint Id = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);
        
        public uint score;
        public uint hitCount;

        public float scale;
        public bool invertX;
        public bool isLeft;
        [FormerlySerializedAs("m_InvertMult")] 
        public float InvertMult;

        public float LeftRight = 1f;
        public float m_velocityMax = 9; // 百米世界纪录不到 10m/s
        public float m_rotateMax = 180f;

        [Tooltip("最佳击球高度")]
        public float BestTargetY = 0.9f;
 
        // /// <summary>
        // /// 网平面交点
        // /// </summary>
        // public Vector3 Intersect;

        /// <summary>
        /// 目标点
        /// </summary>
        public Vector3 Tp;
    
        /// <summary>
        /// 预判击球点
        /// </summary>
        public Vector3 Hp;

        /// <summary>
        /// 时间
        /// </summary>
        public float[] Tt;

        /// <summary>
        /// Inspector 调试用，缓存最佳击球点
        /// </summary>
        public Vector3[] TargetPos;
        
        [Header("v_3:r_4")] public List<float> m_Actions;
    
        public EnvironmentParameters EnvParams => Academy.Instance.EnvironmentParameters;
  
        public Action episodeBeginAction;
        #endregion

        #region MyRegion

        /// <summary>
        /// 计算最佳击球点
        /// </summary>
        /// <out>[0,1,2,3]: 落地前, 第一次落地点, 第一次弹起后, 第二次落地前</out>
        /// <returns type="System.Boolean"></returns>
        /// Unity3D中常用的物理学公式 https://www.cnblogs.com/msxh/p/6128851.html
        /// Unity 如何计算阻力？ https://www.leadwerks.com/community/topic/4385-physics-how-does-unity-calculate-drag/
        /// FIXIT: 求解微积分方程获取精确路径 https://www.zhihu.com/question/68565717
        /// 每次实时获取，不可用缓存
        public bool GetTarget(out Vector3[] outPos, out float[] outTimes)
        {
            // if (lastFloorHit == FloorHit.FloorAHit || lastFloorHit == FloorHit.FloorBHit)
            // {
            //     outPos = TargetPos;
            //     outTimes =Tt;
            //     return outPos.Length > 3;
            // }
            
            // List<Quaternion> tq = new List<Quaternion>();
            // var q = new Quaternion(Vector3.back, Single.Epsilon, );
            var ball = pg.ball;
            var G = pg.G.y;
            var v = ball.Velocity;
            var d = ball.rb.drag; // 0.47 https://www.jianshu.com/p/9da46cf6d5f5
            var m = ball.rb.mass;

            // var ad = - Mathf.Pow(v.magnitude, 2) * drag * v.normalized;

            var ops = new List<Vector3>(4);
            var ots = new List<float>(4);
            var pp = ball.transform.localPosition; // rb.position; //
            var a = new Vector3();
            var tp = pp;
            var time = 5f;
            var dt = 0.009f;//Time.deltaTime;
            // if (dt < 0.01f || dt > 0.3f) dt = 0.01f;
            var timeCount = 0f;
            var bouncec = 0;
            Vector3 minP = pp;
            float minl = float.MaxValue;
            float minPt = 0f;
            for (float t = 0f; t < time && bouncec < 2; t += dt )
            {
                timeCount += dt;
                var ppy = tp.y;
                tp = new Vector3(v.x * dt,v.y * dt,v.z * dt) + pp;

                var l = (tp - transform.localPosition).sqrMagnitude;
                if (l < minl)
                {
                    minl = l;
                    minP = tp;
                    minPt = timeCount;
                }

                if (ppy * tp.y < 0f) // 反弹
                {
                    ++ bouncec;
                    tp.y = 0f;
                    ops.Add(tp);
                    ots.Add(timeCount);
                    v.y = -v.y;
                    // v *= 0.8f; // 非刚性反弹？
                }
                pp = tp;
                
                a.Set(
                    (d * v.x * v.x) / m * (v.x > 0f ? -1f : 1f),
                    (d * v.y * v.y) / m * (v.y > 0f ? -1f : 1f) + G,
                    (d * v.z * v.z) / m * (v.z > 0f ? -1f : 1f));

                var pvy = v.y;
                v.Set(
                    v.x + a.x * dt,
                    v.y + a.y * dt,
                    v.z + a.z * dt);
                
                // 最佳击球点
                var bestHitY = v.x > 0f ? pg.agentA.BestTargetY : pg.agentB.BestTargetY;
                if (   tp.y >= bestHitY && Mathf.Abs(tp.y - bestHitY) < Mathf.Abs(v.y * dt)*1f 
                    || tp.y <  bestHitY && pvy * v.y < 0f) // 顶点
                {
                    ops.Add(tp);
                    ots.Add(timeCount);
                    if (pvy * v.y < 0f) v.y = 0f; 
                }
            } // for
            
            // 最近击球点
            ops.Add(minP);
            ots.Add(minPt);
            
            outTimes = ots.ToArray();
            outPos = ops.ToArray();
            
            TargetPos = outPos;
            Tt = outTimes;
            
            return outPos.Length > 5;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public bool CanIReachPoint(Vector3 p, float t)
        {
            if (   p.x < 0 && invertX 
                || p.x > 0 && !invertX
                || p.y > 2.5f)
                return false;
            
            var s = (p - transform.localPosition).magnitude;
            var mt = Mathf.Abs(s) / m_velocityMax;
            
            // TODO: the ball is passed the point now?
            
            return mt <= t;
        }
        
        public void Wins(float reward = 1)
        {
            ++score;
            SetReward(reward);
            // Partner.SetReward(reward);
            foreach (var i in Adversaries)
            {
                i.SetReward(-reward/2f);
                i.Reset();
            }
            Reset();
        }

        #endregion

        #region Agent

        // [Header("i_1:p_3:r_4:v_3:l_1:lbr_4:bp_3")]
        // public List<float> Observations;
        public override void Initialize() // OnEnable
        {
            InvertMult = invertX ? -1f : 1f;
            LeftRight  = isLeft ? 1f : -1f;
            Id = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 0);
            Reset();
        }


        /// <summary>
        /// <br/>为了使代理学习，观察应包括代理完成其任务所需的所有信息。如果没有足够的相关信息，座席可能会学得不好或根本不会学。
        /// <br/>确定应包含哪些信息的合理方法是考虑计算该问题的分析解决方案所需的条件，或者您希望人类能够用来解决该问题的方法。<br/>
        /// <br/>产生观察
        /// <br/>   ML-Agents为代理提供多种观察方式：
        /// <br/>
        /// <br/>重写Agent.CollectObservations()方法并将观测值传递到提供的VectorSensor。
        /// <br/>将[Observable]属性添加到代理上的字段和属性。
        /// <br/>ISensor使用SensorComponent代理的附件创建来实现接口ISensor。
        /// <br/>Agent.CollectObservations（）
        /// <br/>Agent.CollectObservations（）最适合用于数字和非可视环境。Policy类调用CollectObservations(VectorSensor sensor)每个Agent的 方法。
        /// <br/>此函数的实现必须调用VectorSensor.AddObservation添加矢量观测值。
        /// <br/>该VectorSensor.AddObservation方法提供了许多重载，可将常见类型的数据添加到观察向量中。
        /// <br/>您可以直接添加整数和布尔值，以观测向量，以及一些常见的统一数据类型，如Vector2，Vector3和Quaternion。
        /// <br/>有关各种状态观察功能的示例，您可以查看ML-Agents SDK中包含的 示例环境。例如，3DBall示例使用平台的旋转，球的相对位置和球的速度作为状态观察。
        /// </summary>
        /// <param name="sensor" type="VectorSensor"></param>
        public override void CollectObservations(VectorSensor sensor)
        {
            // self x1
            sensor.AddObservation(Id);

            // playground x16
            sensor.AddObservation(pg.HalfSize);
            sensor.AddObservation(pg.ball.transform.localPosition);    // 球位置 x3
            sensor.AddObservation(pg.ball.rb.velocity);                // 球速度 x3
            sensor.AddObservation(pg.ball.rb.angularVelocity);         // 角速度 x3

            // team A x22
            {
                sensor.AddObservation(Id);
                sensor.AddObservation(InvertMult);
                sensor.AddObservation(transform.localPosition);
                sensor.AddObservation(transform.localEulerAngles);
                sensor.AddObservation(rb.velocity);
                
                sensor.AddObservation(Partner.Id);
                sensor.AddObservation(Partner.InvertMult);
                sensor.AddObservation(Partner.transform.localPosition);
                sensor.AddObservation(Partner.transform.localEulerAngles);
                sensor.AddObservation(Partner.rb.velocity);
            }
            
            // Adversaries 1?2x11
            foreach (var i in Adversaries)
            {
                sensor.AddObservation(i.Id);
                sensor.AddObservation(i.InvertMult);
                sensor.AddObservation(i.transform.localPosition);
                sensor.AddObservation(i.transform.localEulerAngles);
                sensor.AddObservation(i.rb.velocity);
            }
        }

        /**
     * 动作是代理执行的来自策略的指令。当学院调用代理的OnActionReceived()功能时，该操作将作为参数传递给代理。
     * 代理的动作可以采用两种形式之一，即Continuous或Discrete。
     * 当您指定矢量操作空间为Continuous时，传递给Agent的action参数是长度等于该Vector Action Space Size属性的浮点数数组。
     * 当您指定 离散向量动作空间类型时，动作参数是一个包含整数的数组。每个整数都是命令列表或命令表的索引。
     * 在离散向量操作空间类型中，操作参数是索引数组。数组中的索引数由Branches Size属性中定义的分支数确定。
     * 每个分支对应一个动作表，您可以通过修改Branches 属性来指定每个表的大小。
     * 策略和训练算法都不了解动作值本身的含义。训练算法只是为动作列表尝试不同的值，并观察随着时间的推移和许多训练事件对累积奖励的影响。
     * 因此，仅在OnActionReceived()功能中为代理定义了放置动作。
     * 例如，如果您设计了一个可以在两个维度上移动的代理，则可以使用连续或离散矢量动作。
     * 在连续的情况下，您可以将矢量操作大小设置为两个（每个维一个），并且座席的策略将创建一个具有两个浮点值的操作。
     * 在离散情况下，您将使用一个分支，其大小为四个（每个方向一个），并且策略将创建一个包含单个元素的操作数组，其值的范围为零到三。
     * 或者，您可以创建两个大小为2的分支（一个用于水平移动，一个用于垂直移动），并且Policy将创建一个包含两个元素的操作数组，其值的范围从零到一。
     * 请注意，在为代理编程动作时，使用代理Heuristic()方法测试动作逻辑通常会很有帮助，该方法可让您将键盘命令映射到动作。
     */

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            var ball = pg.ball;
            float[] outTime;
            Vector3[] btps;
            if(!GetTarget(out btps, out outTime)) return;
                        
            float[] outTime2;
            Vector3[] btps2;
            var isptcan = Partner.GetTarget(out btps2, out outTime2);
            if(isptcan 
               && outTime2[5] < outTime[5]) return; // partner can do better

            // Intersect = Util.IntersectLineToPlane(
            //     transform.localPosition, 
            //     btps[1] - transform.localPosition, 
            //     Vector3.right, Vector3.zero);

            var continuousActions = actionBuffers.ContinuousActions;
            #if UNITY_EDITOR
            m_Actions = continuousActions.ToList();
            #endif

            int i = 0;
            var velocityX = Mathf.Clamp(continuousActions[i++], -1f, 1f);
            var velocityY = Mathf.Clamp(continuousActions[i++], -1f, 1f);
            var velocityZ = Mathf.Clamp(continuousActions[i++], -1f, 1f);
            var rotateX   = Mathf.Clamp(continuousActions[i++], -1f, 1f);
            var rotateY   = Mathf.Clamp(continuousActions[i++], -1f, 1f);
            var rotateZ   = Mathf.Clamp(continuousActions[i++], -1f, 1f);
            
            var velocity= new Vector3(velocityX, velocityY,velocityZ) * m_velocityMax;
            var rotate  = new Vector3(rotateX, rotateY,rotateZ) * m_rotateMax;

            // // 不干预决策，在 TennisBall 中限制球的运动范围来引导
            // if (playground.agentA.score < playground.levelOne || playground.agentB.score < playground.levelOne)
            // {
            //     rotateX = invertX ? 180f : 0f;
            //     rotateY = 0f;
            //     velocityZ = 0f;
            // }

            // rb.velocity = velocity;
            //
            // rb.rotation = Quaternion.Euler(rotate); // Rigidbody.rotation 比 Transform.rotation 更新旋转速度更快

        }


        public override void Heuristic(in ActionBuffers actionsOut)
        {
            float[] outTime;
            Vector3[] btps;
            if(!GetTarget(out btps, out outTime)) // i can't reach the ball, do nothing
            {
                // Reset();
                return;
            }
            
            float[] outTime2;
            Vector3[] btps2;
            var isptcan = Partner.GetTarget(out btps2, out outTime2);
            if(isptcan && outTime2[5] < outTime[5]) // partner can do better
            {
                Reset();
                return;
            } 
            
            // var offset = transform.rotation.normalized * new Vector3(0.5f, 0f, 0f);
            var p0 = transform.localPosition; // 拍心
            var distance = Vector3.zero;
            for (int i = 0; i < btps.Length; i++)
            {
                if (btps[i].y > 0 && CanIReachPoint(btps[i], outTime[i]))
                {
                    distance = (btps[i] - p0);
                    break;
                }
            }
            if(distance.magnitude < 0.1f)
                distance = Vector3.zero;
            
            rb.velocity = distance.normalized * m_velocityMax;

            var rotation = distance == Vector3.zero  ? Quaternion.identity : Quaternion.LookRotation(distance.normalized, Vector3.right);
            // transform.rotation = rotation;
            // rb.rotation = rotation;

            var continuousActionsOut = actionsOut.ContinuousActions;
            continuousActionsOut[0] = distance.x; // velocityX Racket Movement
            continuousActionsOut[1] = distance.y; // velocityY Racket Jumping
            continuousActionsOut[2] = distance.z; // velocityZ
            continuousActionsOut[3] = rotation.x; // rotateX
            continuousActionsOut[4] = rotation.y; // rotateY
            continuousActionsOut[5] = rotation.z; // rotateZ

            // continuousActionsOut[0] = Input.GetAxis("Horizontal");              // moveX Racket Movement
            // continuousActionsOut[1] = Input.GetKey(KeyCode.Space) ? 1f : 0f;    // moveY Racket Jumping
            // continuousActionsOut[2] = Input.GetAxis("Vertical");                // moveZ
            // if(SystemInfo.supportsGyroscope)
            // {
            //     var ang = Input.gyro.attitude.eulerAngles;
            //     continuousActionsOut[3] = Input.gyro.attitude.x; // rotateX
            //     continuousActionsOut[4] = Input.gyro.attitude.y; // rotateY
            //     continuousActionsOut[5] = Input.gyro.attitude.z; // rotateZ
            //     // continuousActionsOut[6] = Input.gyro.attitude.w; // rotateW
            // }
            // else
            // {
            //     continuousActionsOut[0] = Random.Range(-1f, 1f); // moveX Racket Movement
            //     continuousActionsOut[1] = Random.Range(-1f, 1f); // moveY Racket Jumping
            //     continuousActionsOut[2] = Random.Range(-1f, 1f); // moveZ
            //     continuousActionsOut[3] = Random.Range(-1f, 1f); // rotateX
            //     continuousActionsOut[4] = Random.Range(-1f, 1f); // rotateY
            //     continuousActionsOut[5] = Random.Range(-1f, 1f); // rotateZ
            //     // continuousActionsOut[6] = Random.Range(-1f, 1f); // rotateW
            // }

        }


        public override void OnEpisodeBegin()
        {
            Reset();

            if (episodeBeginAction != null)
                episodeBeginAction();
        }

        public void Reset()
        {
            transform.localPosition = new Vector3(
                -InvertMult * 9f,
                1f,
                LeftRight * InvertMult * 2f);

            rb.velocity = new Vector3(0f, 0f, 0f);
            rb.rotation = Quaternion.Euler(new Vector3(
                invertX ? 180f : 0f,
                0f,
                -70f
            ));
        }

        #endregion //Agent

        #region MonoBehaviour

        // private void FixedUpdate()
        // {
        //     var p = transform.localPosition; // GetBallTargetP(); //
        //     // var rp = rigidbody.position;
        //     transform.localPosition = new Vector3(
        //         Mathf.Clamp(p.x, 2f * (invertX ? 0f : -playground.Size.x), 2f * (invertX ? playground.Size.x : 0f)),
        //         Mathf.Clamp(p.y, 0.1f, 3f),
        //         Mathf.Clamp(p.z, -2f * playground.Size.z, 2f * playground.Size.z));
        //     
        //     // rigidbody.rotation = Quaternion.Euler(new Vector3(
        //     //     invertX ? 180f : 0f,
        //     //     0f,
        //     //     -55f
        //     // ));
        // }

        #endregion //MonoBehaviour
    }
}